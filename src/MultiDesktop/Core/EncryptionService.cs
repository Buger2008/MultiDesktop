using PostQuantum.FileEncryption;

namespace MultiDesktop.Core
{
    /// <summary>
    /// 桌面加解密：压缩 → 加密 → 删除明文文件夹；解密还原；移除加密。
    /// 不依赖 WinForms：密码错误以 PqDecryptionException 抛出，由调用方决定如何提示。
    /// </summary>
    public static class EncryptionService
    {
        /// <summary>加密压缩包存放目录。</summary>
        public static string ZipsDir => AppPaths.ZipsDir;

        /// <summary>本次会话内已通过验证的密码缓存（桌面名称 -> 密码），用于离开桌面时自动重新加密。</summary>
        private static readonly Dictionary<string, string> SessionPasswords = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>加密包路径。</summary>
        public static string EncryptedPath(int id) => Path.Combine(ZipsDir, id + ".zip.encrypted");

        /// <summary>明文临时压缩包路径。</summary>
        public static string PlainZipPath(int id) => Path.Combine(ZipsDir, id + ".zip");

        /// <summary>
        /// 由桌面名称生成稳定的正整数 id（用于命名加密压缩包与定位密码记录），
        /// 不依赖行索引，删除/重排桌面不会导致 id 错位。
        /// </summary>
        public static int GetZipId(string desktopName)
        {
            uint hash = 2166136261; // FNV-1a
            foreach (char c in desktopName)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)(hash % int.MaxValue);
        }

        /// <summary>该桌面是否已存在加密压缩包（即是否已设置过密码）。</summary>
        public static bool HasEncryptedFile(int id) => File.Exists(EncryptedPath(id));

        /// <summary>
        /// 通过实际解密验证密码是否正确（解密到临时目录后立即清理，不落盘明文）：
        /// 密码错误抛出 PqDecryptionException；加密包不存在时视为通过（异常状态，由后续流程重建）。
        /// </summary>
        public static async Task VerifyPasswordByDecryptAsync(int id, string password)
        {
            string zipfile = PlainZipPath(id);
            string encryptedfile = EncryptedPath(id);
            if (!File.Exists(encryptedfile))
                return; // 无加密包：无法验证，交由后续流程处理
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("密码不能为空");

            string tempDir = Path.Combine(ZipsDir, ".verify_" + id);
            DeleteIfExists(zipfile);
            try
            {
                Directory.CreateDirectory(tempDir);
                await new PqFileDecryptor().DecryptFileAsync(encryptedfile, zipfile, password);
                // 能解出内容即证明密码正确；再解压到临时目录验证压缩包完整性
                System.IO.Compression.ZipFile.ExtractToDirectory(zipfile, tempDir, true);
            }
            finally
            {
                DeleteIfExists(zipfile);
                if (Directory.Exists(tempDir))
                {
                    ClearReadOnlyAttributes(tempDir);
                    Directory.Delete(tempDir, true);
                }
            }
        }

        // ===== 会话密码缓存 =====
        public static string? GetSessionPassword(string? name)
            => !string.IsNullOrEmpty(name) && SessionPasswords.TryGetValue(name, out var pw) ? pw : null;

        public static void SetSessionPassword(string? name, string password)
        {
            if (!string.IsNullOrEmpty(name)) SessionPasswords[name] = password;
        }

        /// <summary>
        /// 加密桌面文件夹：压缩 → 加密压缩包 → 删除原文件夹。
        /// 文件夹只会在压缩与加密全部成功后才会被删除，任何一步失败都保留原文件夹，不会丢失数据。
        /// </summary>
        public static async Task GetZipFile(string folder, int id, string password)
        {
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException($"桌面文件夹不存在：{folder}");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("密码不能为空");

            // 预检被占用/无权限的文件，避免压缩中途因权限不足、文件占用而失败
            var locked = GetLockedFiles(folder);
            if (locked.Count > 0)
                throw new IOException(
                    "以下文件正被其他程序占用或无法访问，请先关闭相关程序后重试：\r\n" + string.Join("\r\n", locked));

            Directory.CreateDirectory(ZipsDir);
            string zipfile = PlainZipPath(id);
            string encryptedfile = EncryptedPath(id);
            DeleteIfExists(zipfile);
            DeleteIfExists(encryptedfile);

            try
            {
                // 1. 压缩（明文临时文件）
                System.IO.Compression.ZipFile.CreateFromDirectory(folder, zipfile, System.IO.Compression.CompressionLevel.Optimal, false);
                // 2. 加密压缩包 —— 必须 await 等待异步任务真正完成，否则后续删除会因文件占用而失败
                await new PqFileEncryptor().EncryptFileAsync(zipfile, encryptedfile, password);
                if (!File.Exists(encryptedfile))
                    throw new IOException("加密失败：未生成加密文件");
                // 3. 删除明文临时压缩包
                File.Delete(zipfile);
                // 4. 删除原文件夹（先清除只读属性）
                ClearReadOnlyAttributes(folder);
                Directory.Delete(folder, true);
            }
            catch
            {
                // 任何失败都保留原文件夹，仅清理明文临时压缩包
                DeleteIfExists(zipfile);
                throw;
            }
        }

        /// <summary>
        /// 解密加密压缩包并还原桌面文件夹。保留加密包，便于下次再次解锁；
        /// 密码错误时解密库会抛出异常，且不会产生任何输出文件。
        /// </summary>
        public static async Task UnZipFile(string folder, int id, string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("密码不能为空");

            string zipfile = PlainZipPath(id);
            string encryptedfile = EncryptedPath(id);
            if (!File.Exists(encryptedfile))
                throw new FileNotFoundException("未找到加密文件，无法解锁", encryptedfile);

            DeleteIfExists(zipfile);
            try
            {
                await new PqFileDecryptor().DecryptFileAsync(encryptedfile, zipfile, password);
                if (!File.Exists(zipfile))
                    throw new IOException("解密失败：未生成压缩包");
                // 解密成功后才创建目标文件夹：若密码错误，避免留下空文件夹
                // （否则后续会被误判为“已解密”，跳过真实还原甚至覆盖原加密包）
                Directory.CreateDirectory(folder);
                System.IO.Compression.ZipFile.ExtractToDirectory(zipfile, folder, true);
                File.Delete(zipfile);
            }
            catch
            {
                DeleteIfExists(zipfile);
                throw;
            }
        }

        /// <summary>
        /// 移除加密：无论文件夹是否存在，都先通过实际解密验证原密码（错误则抛 PqDecryptionException），
        /// 然后删除加密包。文件夹不存在时顺带真实解密还原明文。
        /// </summary>
        public static async Task RemoveEncryption(string folder, int id, string password)
        {
            if (Directory.Exists(folder))
            {
                // 文件夹为明文：解密到临时目录验证密码，避免旧压缩包覆盖新文件
                await VerifyPasswordByDecryptAsync(id, password);
            }
            else
            {
                // 文件夹不存在（仍加密）：真实解密还原，密码错误在此抛出
                await UnZipFile(folder, id, password);
            }
            DeleteIfExists(EncryptedPath(id));
        }

        // ===== 内部工具 =====

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }

        private static void ClearReadOnlyAttributes(string folder)
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            foreach (var dir in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories))
                File.SetAttributes(dir, FileAttributes.Normal);
        }

        /// <summary>找出被其他进程独占或无法访问的文件（重试 3 次，容忍杀毒软件等短暂占用）。</summary>
        private static List<string> GetLockedFiles(string folder)
        {
            var locked = new List<string>();
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                bool canOpen = false;
                for (int attempt = 0; attempt < 3 && !canOpen; attempt++)
                {
                    try
                    {
                        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                        canOpen = true;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(200);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        locked.Add($"{file}（无访问权限）");
                        canOpen = true;
                    }
                }
                if (!canOpen)
                    locked.Add(file);
            }
            return locked;
        }
    }
}
