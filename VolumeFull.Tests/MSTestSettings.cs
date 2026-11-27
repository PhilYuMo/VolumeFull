// Config 的读写在同一个 data\config.ini 上，测试之间不能并行，否则会互相覆盖。
[assembly: DoNotParallelize]
