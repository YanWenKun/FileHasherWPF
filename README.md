# File Hasher WPF

简单方便的文件哈希工具 Quick Tool for Hashing Files

## 使用

![](screenshot.webp)

直接将文件拖入窗口即可，支持多个文件。

## 关于代码

Translation can be done easily. Strings are extracted.

平时经常会计算文件哈希值，但又不是每次都值得开个大家伙来完成，索性写了个小工具，顺便学习一下WPF。

本例中的核心代码其实就几行：

```csharp
FileStream fs = File.OpenRead(filePath); //打开文件
HashAlgorithm hash = HashAlgorithm.Create(hashType); //建立哈希对象
byte[] result = hash.ComputeHash(fs); //计算并返回结果
return BitConverter.ToString(result).Replace("-", string.Empty); //将二进制结果转换成字符串
```

剩下一大堆便是围绕这几行代码展开的。

#### 一些可改进的地方

- 用到了WPF的XAML布局，但没做数据绑定
- 队列机制与取消机制比较简单粗暴，没怎么利用Task（WhenAll、WhenAny、CancellationToken，etc.）
- 如果做了数据绑定，可以进一步利用事件机制
- 进一步提取文本并生成多语言资源等

不过既然核心功能表现良好，就不折腾了。学习的事情，留给新项目吧。

#### ~~.NET Core 3.1 WPF~~ （已迁移到 .NET 5）

代码已迁移到 .NET Core 3.1，纯属尝鲜，因为程序简单所以没有遇到什么困难。

但是 .NET Core 3.1 生成出来的EXE文件较大（~158KiB），且需要附带 DLL、PDB、JSON 文件（~32 KiB）。如果自带运行时而打包成单个 EXE，则颇为硕大（~85 MiB）。

相比之下，基于 .NET Framework 生成的文件只有27KiB~~，且启动更快，所以目前发布下载的仍然是老版本EXE~~。

期待未来的 .NET 5 时代。

#### .NET 5 WPF

简单测试， .NET 5 下 SHA-512 哈希速度，肉眼可见快于 .NET Framework 4.0 版。
属于对开发者“免费”的性能提升，值得升级。

原因调查： [.NET Cryptography Model](https://docs.microsoft.com/en-us/dotnet/standard/security/cryptography-model)

 > In .NET Core and .NET 5 and later versions, all implementation classes (*CryptoServiceProvider, *Managed, and *Cng) are wrappers for the operating system (OS) algorithms. If the OS algorithms are FIPS-certified, then .NET uses FIPS-certified algorithms.

看起来性能的提升应该归功于 Windows 10。

#### .NET 6

升级十分顺利。性能部分尚未测试。

#### .NET 8

升级到 8 十分平滑而简易。
`HashAlgorithm.Create()` 方法已过时，留待下次（可能添加新哈希算法）一并更新。
