using BenchmarkDotNet.Running;
using s3kira.Benchmark;

//BenchmarkRunner.Run<UploadFileBenchmark>();
//BenchmarkRunner.Run<DownloadFileBenchmark>();
//BenchmarkRunner.Run<BucketExistsBenchmark>();
BenchmarkRunner.Run<FullBenchmark>();