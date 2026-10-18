using System;
using System.Collections.Generic;
using Silk.NET.Core.Native;
using Silk.NET.DXGI;

namespace Xilium.CefGlue.Headless
{
    public static class GpuAdapterSelection
    {
        public static unsafe KeyValuePair<string, string>[] TryGetIntelAdapterLuidFlags()
        {
            try
            {
                var dxgi = DXGI.GetApi(null);
                var factoryGuid = IDXGIFactory1.Guid;
                void* factoryPointer = null;
                SilkMarshal.ThrowHResult(dxgi.CreateDXGIFactory1(&factoryGuid, &factoryPointer));
                var factory = (IDXGIFactory1*)factoryPointer;

                try
                {
                    var detected = new List<string>();
                    string intelName = null;
                    int intelHigh = 0;
                    uint intelLow = 0;

                    uint i = 0;
                    while (true)
                    {
                        IDXGIAdapter1* candidate = null;
                        var hr = factory->EnumAdapters1(i, &candidate);
                        if (hr < 0 || candidate == null)
                        {
                            break;
                        }

                        try
                        {
                            AdapterDesc1 desc;
                            candidate->GetDesc1(&desc);
                            var name = SilkMarshal.PtrToString((IntPtr)desc.Description, NativeStringEncoding.LPWStr);
                            detected.Add(name);

                            if (intelName == null && name != null && name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                            {
                                intelName = name;
                                intelHigh = desc.AdapterLuid.High;
                                intelLow = desc.AdapterLuid.Low;
                            }
                        }
                        finally
                        {
                            candidate->Release();
                        }

                        i++;
                    }

                    Console.WriteLine($"GPU: detected {detected.Count} adapter(s): {string.Join(", ", detected)}");

                    if (intelName == null)
                    {
                        Console.WriteLine("GPU: no Intel adapter found - CEF keeps its own default GPU choice (QSV hardware encode needs an Intel GPU to actually accelerate on).");
                        return null;
                    }

                    Console.WriteLine($"GPU: using '{intelName}' for CEF's own rendering (forced via use-adapter-luid, so its accelerated-paint texture lands on the same adapter QSV/Quick Sync hardware encode needs).");
                    return new[]
                    {
                        new KeyValuePair<string, string>("use-angle", "d3d11"),
                        new KeyValuePair<string, string>("use-adapter-luid", $"{intelHigh},{intelLow}"),
                    };
                }
                finally
                {
                    factory->Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GPU: adapter enumeration failed, leaving CEF's own GPU adapter choice untouched: {ex.Message}");
                return null;
            }
        }
    }
}
