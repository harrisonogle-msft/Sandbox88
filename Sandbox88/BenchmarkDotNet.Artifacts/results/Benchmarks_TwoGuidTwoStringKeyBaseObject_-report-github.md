```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.2894)
AMD Ryzen Threadripper PRO 3955WX 16-Cores, 1 CPU, 32 logical and 16 physical cores
.NET SDK 9.0.102
  [Host] : .NET 8.0.12 (8.0.1224.60305), X64 RyuJIT AVX2


```
| Method    | Mean | Error | Ratio | RatioSD | Alloc Ratio |
|---------- |-----:|------:|------:|--------:|------------:|
| Baseline  |   NA |    NA |     ? |       ? |           ? |
| Sorted    |   NA |    NA |     ? |       ? |           ? |
| ZeroAlloc |   NA |    NA |     ? |       ? |           ? |

Benchmarks with issues:
  Benchmarks<TwoGuidTwoStringKeyBaseObject>.Baseline: DefaultJob
  Benchmarks<TwoGuidTwoStringKeyBaseObject>.Sorted: DefaultJob
  Benchmarks<TwoGuidTwoStringKeyBaseObject>.ZeroAlloc: DefaultJob
