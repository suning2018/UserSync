using System;
using System.Security.Cryptography;
using System.Text;

namespace UserSync.Services
{
    /// <summary>
    /// 密码加密辅助类
    /// 实现与系统一致的密码加密逻辑：MD5 -> DES -> MD5
    /// </summary>
    public static class PasswordEncryptionHelper
    {
        /// <summary>
        /// 生成随机密钥（16位小写MD5）
        /// 实现逻辑与 JUWX.Utility.Common.CreateNo() 一致：
        /// CreateNo() 生成：DateTime.Now.ToString("yyyyMMddHHmmss") + Random.Next(1000, 10000)
        /// 然后 MD5 加密取前16位，转小写
        /// </summary>
        public static string GenerateSecretKey()
        {
            // 生成随机编号（与 JUWX.Utility.Common.CreateNo() 逻辑一致）
            Random random = new Random();
            string strRandom = random.Next(1000, 10000).ToString(); // 生成编号 1000-9999
            string code = DateTime.Now.ToString("yyyyMMddHHmmss") + strRandom; // 形如：202501121430451234
            
            // MD5 加密取前16位，转小写（与 JUWX.Utility.Md5.md5(code, 16).ToLower() 一致）
            return ComputeMD5(code, 16).ToLower();
        }

        /// <summary>
        /// 加密密码（三步加密：MD5 -> DES -> MD5）
        /// 实现逻辑与 JUWX.Utility 一致：
        /// JUWX.Utility.Md5.md5(JUWX.Utility.DESEncrypt.Encrypt(JUWX.Utility.Md5.md5(plainPassword, 32).ToLower(), secretKey).ToLower(), 32).ToLower()
        /// </summary>
        /// <param name="plainPassword">明文密码</param>
        /// <param name="secretKey">加密密钥（16位）</param>
        /// <returns>加密后的密码</returns>
        public static string EncryptPassword(string plainPassword, string secretKey)
        {
            if (string.IsNullOrEmpty(plainPassword))
            {
                throw new ArgumentException("密码不能为空", nameof(plainPassword));
            }

            if (string.IsNullOrEmpty(secretKey))
            {
                throw new ArgumentException("密钥不能为空", nameof(secretKey));
            }

            // 第一步：对原始密码进行MD5加密（32位），转小写
            // 对应：JUWX.Utility.Md5.md5(json.F_UserPassword, 32).ToLower()
            string step1 = ComputeMD5(plainPassword, 32).ToLower();

            // 第二步：使用密钥进行DES加密，转小写
            // 对应：JUWX.Utility.DESEncrypt.Encrypt(step1, secretKey).ToLower()
            string encrypted = DESEncrypt(step1, secretKey).ToLower();

            // 第三步：对加密结果再次进行MD5加密（32位），转小写
            // 对应：JUWX.Utility.Md5.md5(encrypted, 32).ToLower()
            string finalPassword = ComputeMD5(encrypted, 32).ToLower();

            return finalPassword;
        }

        /// <summary>
        /// 计算MD5哈希值
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <param name="length">输出长度（16或32）</param>
        /// <returns>MD5哈希值</returns>
        private static string ComputeMD5(string input, int length = 32)
        {
            using (MD5 md5 = MD5.Create())
            {
                // 使用 UTF8 编码（与 JUWX.Utility.Md5.md5 一致）
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }

                string result = sb.ToString();
                // 根据长度截取
                if (length == 16)
                {
                    // 取中间16位（从第8位开始，取16位）
                    // 例如：28b6f6099c0db610... -> 9c0db610...（从索引8开始）
                    return result.Substring(8, 16);
                }
                return result; // 返回32位
            }
        }

        /// <summary>
        /// DES加密
        /// 实现逻辑与 JUWX.Utility.DESEncrypt.Encrypt() 一致：
        /// 1. 使用密钥的MD5哈希的前8位作为DES密钥和IV
        /// 2. 使用 Encoding.Default 编码输入文本
        /// 3. 输出十六进制大写字符串（不是Base64）
        /// </summary>
        /// <param name="plainText">明文</param>
        /// <param name="key">密钥（16位）</param>
        /// <returns>加密后的十六进制字符串（大写）</returns>
        private static string DESEncrypt(string plainText, string key)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            // 计算密钥的MD5哈希（对应 FormsAuthentication.HashPasswordForStoringInConfigFile(sKey, "md5")）
            // FormsAuthentication.HashPasswordForStoringInConfigFile 使用 UTF8 编码计算MD5，返回大写十六进制字符串
            string keyMd5 = ComputeMD5(key, 32).ToUpper(); // MD5哈希，转大写（与原始代码一致）
            
            // 取MD5哈希的前8个字符作为DES密钥和IV（对应 .Substring(0, 8)）
            // 注意：这里是取前8个字符（比如 "A1B2C3D4"），然后用ASCII编码转换为8字节
            string keySubstring = keyMd5.Substring(0, 8);
            byte[] keyBytes = Encoding.ASCII.GetBytes(keySubstring);
            byte[] ivBytes = Encoding.ASCII.GetBytes(keySubstring);

            // 使用 Encoding.Default 编码输入文本（对应 Encoding.Default.GetBytes(Text)）
            byte[] inputByteArray = Encoding.Default.GetBytes(plainText);

            using (var des = System.Security.Cryptography.DES.Create())
            {
                des.Key = keyBytes;
                des.IV = ivBytes;
                // DESCryptoServiceProvider 默认使用 CBC 模式，不是 ECB
                // 但原始代码没有显式设置Mode，所以使用默认值（CBC）
                des.Mode = CipherMode.CBC;
                des.Padding = PaddingMode.PKCS7;

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(inputByteArray, 0, inputByteArray.Length);
                        cs.FlushFinalBlock();
                    }

                    // 转换为十六进制大写字符串（对应 {0:X2} 格式）
                    StringBuilder ret = new StringBuilder();
                    foreach (byte b in ms.ToArray())
                    {
                        ret.AppendFormat("{0:X2}", b);
                    }
                    return ret.ToString();
                }
            }
        }
    }
}
