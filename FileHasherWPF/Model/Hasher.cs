using System;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;

namespace FileHasherWPF.Model
{

    /// <summary>
    /// Hasher的抽象类
    /// </summary>
    public abstract class Hasher
    {
        public struct STATUS
        {
            public const string HASH_EQUAL = "校验值相同";
            public const string SUCCESS = "恭喜";
            public const string HASH_UNEQUAL = "校验值不同！";
            public const string CAUTION = "注意！";
            public const string FILE_ERROR = "文件读取错误！";
            public const string HASH_INCOMPL = "已取消文件读取";
        }

        public enum HashAlgos
        {
            MD5,
            SHA1,
            SHA256,
            SHA512
        }

        // 此处使用了自动属性，因而不再需要私有成员
        public string HashAlgo { get; }

        public string Input { get; }

        public string HashResult { get; protected set; }

        // 构造函数中的自动属性
        public Hasher(HashAlgos algo, string input)
        {
            HashAlgo = algo switch
            {
                HashAlgos.MD5 => "MD5",
                HashAlgos.SHA1 => "SHA1",
                HashAlgos.SHA256 => "SHA256",
                HashAlgos.SHA512 => "SHA512",
                _ => "SHA256",
            };
            Input = input;
            HashResult = "";
        }

        /// <summary>
        /// 将字节数组格式化到字符串
        /// </summary>
        /// <param name="b">byte型数组</param>
        /// <returns>去掉连接符后的十六进制数字符串</returns>
        protected static string FormatBytes(byte[] b)
        {
            // 该方法不是解码，而是将HEX“音译”到字符串，1A->"1A"
            string s = BitConverter.ToString(b);
            s = s.Replace("-", string.Empty);
            return s;
        }
    }

    /// <summary>
    /// 对字符串进行哈希计算
    /// </summary>
    public class StringHasher : Hasher
    {
        public StringHasher(HashAlgos algo, string input) : base(algo, input)
        {
            HashResult = GetStringHash(HashAlgo, Input);
        }

        public static string GetStringHash(string hashType, string s)
        {
            // 将字符串转为字节数组
            byte[] byteArr = Encoding.Default.GetBytes(s);
            // 方法HashAlgorithm.Create()直接以字符串作为参数来选择算法类型，非常方便
            // 目前版本中，SHA2家族算法默认由托管实现，SHA1与MD5由CSP实现，即Windows内置的受到FIPS即美国政府认证的安全实现
            // SHA2家族亦有CSP/Cng实现，不同实现的性能有待测试，暂不折腾
            HashAlgorithm hash = HashAlgorithm.Create(hashType);
            // 计算结果，并转为字符串返回
            byte[] result = hash.ComputeHash(byteArr);
            return FormatBytes(result);
        }
    }

    /// <summary>
    /// 对文件进行哈希计算（异步）
    /// </summary>
    public class FileHasher : Hasher
    {
        public string FilePath { get; }
        public string FileName { get; }
        public long FileLength { get; }

        public long CurrentBytesPosition => GetCurrentBytesPosition();

        private FileStream FS { get; }

        /// <summary>
        /// 对文件进行哈希运算
        /// </summary>
        /// <param name="algo">指定哈希算法</param>
        /// <param name="input">指定文件路径</param>
        public FileHasher(HashAlgos algo, string input) : base(algo, input)
        {
            // 获取文件名是纯字符串操作，不会抛出文件系统异常。错误的文件名返回空串
            FilePath = Path.GetFullPath(Input);
            FileName = Path.GetFileName(FilePath);
            HashResult = STATUS.HASH_INCOMPL;
            try
            {
                // 以只读模式打开，不指定进程共享（独占）参数、异步读取参数
                // 因为写在try中，所以不必using(){}的用法，但需要注意Dispose()
                FS = File.OpenRead(FilePath);
                //FS = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
                FileLength = FS.Length;
            }
            catch
            {
                HashResult = STATUS.FILE_ERROR;
                FileLength = 0L;
                // 读取过程中并不隐含Dispose()方法，但是GC会自动回收（有延迟）
                FS?.Dispose();
            }
        }

        /// <summary>
        /// 开始计算文件哈希值
        /// </summary>
        public async Task StartHashFile()
        {
            if (FS != null && HashResult != STATUS.FILE_ERROR)
            {
                // 异步的标准用法之一，很关键哦
                await Task.Run(() =>
                {
                    try
                    {
                        HashAlgorithm hash = HashAlgorithm.Create(HashAlgo);
                        byte[] result = hash.ComputeHash(FS);
                        HashResult = FormatBytes(result);
                    }
                    catch
                    {
                        HashResult = STATUS.FILE_ERROR;
                    }
                    finally
                    {
                        FS?.Dispose();
                    }
                });
            }
        }

        /// <summary>
        /// 取消当前任务（如果任务存在）
        /// </summary>
        public void Stop()
        {
            // 这里的写法非常简单粗暴，直接关闭文件流，忽略异常
            // 正常的写法应当是使用 CancellationTokenSource 及其 Token，
            // 在循环中使用buffer读取文件，在CTS.Cancel()后跳出循环
            if (FS != null && HashResult == STATUS.HASH_INCOMPL)
                FS.Dispose();
        }

        /// <summary>
        /// 获取当前文件读取字节位置，如异常则返回文件长度（默认为0）
        /// </summary>
        private long GetCurrentBytesPosition()
        {
            // 无法直接得知IDisposable是否已被Dispose()，可catch异常，
            // 或额外用个bool挂旗，或进一步override Dispose()方法等
            try
            {
                return FS.Position;
            }
            catch
            {
                return FileLength;
            }
        }

    }

}
