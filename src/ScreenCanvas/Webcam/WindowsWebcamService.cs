using System.Runtime.InteropServices;

namespace ScreenCanvas.Webcam;

/// <summary>Discovers DirectShow cameras without external packages. Frame capture is supplied by the recording backend.</summary>
public sealed class WindowsWebcamService : IWebcamService
{
    private readonly List<WebcamDevice> _devices = [];
    public WebcamCapability Capability { get; private set; } = new(true, false, "Camera frame capture requires a Media Foundation recording backend.");
    public IReadOnlyList<WebcamDevice> Devices => _devices;
    public string? SelectedDeviceId { get; set; }
    public bool IsRunning { get; private set; }
    public event EventHandler<System.Windows.Media.Imaging.BitmapSource>? FrameReady;

    public Task RefreshDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); _devices.Clear();
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("62BE5D10-60EB-11D0-BD3B-00A0C911CE86"), true)!;
            var enumerator = (ICreateDevEnum)Activator.CreateInstance(type)!;
            var category = new Guid("860BB310-5D01-11D0-BD3B-00A0C911CE86");
            if (enumerator.CreateClassEnumerator(ref category, out var monikers, 0) == 0 && monikers is not null)
            {
                var items = new IMoniker[1];
                while (monikers.Next(1, items, IntPtr.Zero) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var bagId = typeof(IPropertyBag).GUID; items[0].BindToStorage(null, null, ref bagId, out var bagObject);
                    var bag = (IPropertyBag)bagObject; bag.Read("FriendlyName", out var name, IntPtr.Zero);
                    items[0].GetDisplayName(null, null, out var id); _devices.Add(new WebcamDevice(id, name?.ToString() ?? "Camera"));
                    Marshal.ReleaseComObject(bag); Marshal.ReleaseComObject(items[0]);
                }
                Marshal.ReleaseComObject(monikers);
            }
            Marshal.ReleaseComObject(enumerator);
        }
        catch (COMException ex) { Capability = new(false, false, ex.Message); }
        SelectedDeviceId ??= _devices.FirstOrDefault()?.Id;
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException(Capability.Limitation);
    public Task StopAsync() { IsRunning = false; return Task.CompletedTask; }
    public void ReportFrame(System.Windows.Media.Imaging.BitmapSource frame) => FrameReady?.Invoke(this, frame);
    public void Dispose() { IsRunning = false; FrameReady = null; }

    [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICreateDevEnum { [PreserveSig] int CreateClassEnumerator(ref Guid category, out IEnumMoniker? enumerator, int flags); }
    [ComImport, Guid("00000102-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumMoniker { [PreserveSig] int Next(int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IMoniker[] monikers, IntPtr fetched); void Skip(int count); void Reset(); void Clone(out IEnumMoniker clone); }
    [ComImport, Guid("0000000F-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMoniker { void GetClassID(out Guid clsid); void IsDirty(); void Load(IntPtr stream); void Save(IntPtr stream, bool clearDirty); void GetSizeMax(out long size); void BindToObject(object? bindContext, object? left, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object value); void BindToStorage(object? bindContext, object? left, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object value); void Reduce(object? bindContext, int reduce, ref IMoniker? left, out IMoniker reduced); void ComposeWith(IMoniker right, bool onlyIfNotGeneric, out IMoniker composite); void Enum(bool forward, out IEnumMoniker enumerator); void IsEqual(IMoniker other); void Hash(out int hash); void IsRunning(object? bindContext, IMoniker? left, IMoniker? newlyRunning); void GetTimeOfLastChange(object? bindContext, IMoniker? left, out long time); void Inverse(out IMoniker inverse); void CommonPrefixWith(IMoniker other, out IMoniker prefix); void RelativePathTo(IMoniker other, out IMoniker relative); void GetDisplayName(object? bindContext, IMoniker? left, [MarshalAs(UnmanagedType.LPWStr)] out string displayName); void ParseDisplayName(object? bindContext, IMoniker? left, string name, out int eaten, out IMoniker output); void IsSystemMoniker(out int type); }
    [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyBag { void Read([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.Struct)] out object value, IntPtr errorLog); void Write(string name, ref object value); }
}
