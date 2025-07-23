using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace Common.Utilities
{
    public class CryptoUtils
    {
        /// <summary>
        /// 计算字符串的MD5值
        /// </summary>
        public static string MD5(string source)
        {
            var md5 = new MD5CryptoServiceProvider();
            var data = Encoding.UTF8.GetBytes(source);
            var md5Data = md5.ComputeHash(data, 0, data.Length);
            md5.Clear();

            var destString = "";
            foreach (var t in md5Data)
            {
                destString += Convert.ToString(t, 16).PadLeft(2, '0');
            }

            destString = destString.PadLeft(32, '0');
            return destString;
        }

        /// <summary>
        /// 计算文件的MD5值
        /// </summary>
        public static string MD5File(string file)
        {
            try
            {
                var fs = new FileStream(file, FileMode.Open);
                MD5 md5 = new MD5CryptoServiceProvider();
                var retVal = md5.ComputeHash(fs);
                fs.Close();

                var sb = new StringBuilder();
                foreach (var t in retVal)
                {
                    sb.Append(t.ToString("x2"));
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("MD5File() fail, error:" + ex.Message);
            }
        }

        /// <summary>
        /// 将字符串编码为Base64格式
        /// </summary>
        /// <param name="input">输入的字符串</param>
        /// <returns>Base64字符串</returns>
        public static string EncodeBase64(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// 解码Base64格式的字符串
        /// </summary>
        /// <param name="input">Base64字符串</param>
        /// <returns>解码后的字符串</returns>
        public static string DecodeBase64(string input)
        {
            var inputBytes = Convert.FromBase64String(input);
            return Encoding.UTF8.GetString(inputBytes);
        }
    }
}