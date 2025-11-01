using System.Runtime.InteropServices;
using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;

namespace ConsoleApp1;

using Silk.NET.Maths;
using Silk.NET.Windowing;

public unsafe class Engine : IDisposable
{
    // Window object
    private IWindow window;
    private WebGPU wgpu;
    private Instance* instance = null;
    private Surface* surface = null;
    private Adapter* adapter = null;
    private Device* device = null;

    // Pipeline queue
    private Queue* queue = null;
    private CommandEncoder* currentEncoder = null;
    private RenderPassEncoder* currentRenderPass = null;
    private SurfaceTexture surfaceTexture;
    private TextureView* surfaceTextureView = null;

    public void Initialize()
    {
        WindowOptions options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1280, 720),
            Title = "FPS Game",
            API = GraphicsAPI.None
        };

        window = Window.Create(options);
        window.Initialize();

        // Setup WGPU
        CreateApi();
        CreateInstance();
        CreateSurface();
        CreateAdapter();
        CreateDevice();
        ConfigureSurface();
        ConfigureDebugCallback();

        window.Load += OnLoad;
        window.Update += OnUpdate;
        window.Render += OnRender;

        window.Run();
    }

    private void CreateApi()
    {
        wgpu = WebGPU.GetApi();
        Console.WriteLine("WGPU API : It loaded");
    }

    private void CreateInstance()
    {
        InstanceDescriptor descriptor = new InstanceDescriptor();
        instance = wgpu.CreateInstance(descriptor);
        Console.WriteLine("WGPU Instance Created");
    }

    private void CreateSurface()
    {
        surface = window.CreateWebGPUSurface(wgpu, instance);
        Console.WriteLine("WGPU Surface Created");
    }

    private void CreateAdapter()
    {
        RequestAdapterOptions options = new RequestAdapterOptions
        {
            CompatibleSurface = surface,
            BackendType = BackendType.Vulkan,
            PowerPreference = PowerPreference.HighPerformance
        };

        PfnRequestAdapterCallback callback = PfnRequestAdapterCallback.From(
            (status, wgpuAdapter, msgPtr, userDataPtr) =>
            {
                if (status == RequestAdapterStatus.Success)
                {
                    this.adapter = wgpuAdapter;
                    Console.WriteLine("Adapter Created");
                }
                else
                {
                    string msg = Marshal.PtrToStringAnsi((IntPtr)msgPtr);
                    Console.WriteLine($"Adapter Failed: Could not find WGPU adapter {msg}");
                }
            });

        wgpu.InstanceRequestAdapter(instance, options, callback, null);
    }

    private void CreateDevice()
    {
        PfnRequestDeviceCallback callback = PfnRequestDeviceCallback.From(
            (status, wgpuDevice, msgPtr, userDataPtr) =>
            {
                if (status == RequestDeviceStatus.Success)
                {
                    this.device = wgpuDevice;
                    Console.WriteLine("Device Created");
                }
                else
                {
                    string msg = Marshal.PtrToStringAnsi((IntPtr)msgPtr);
                    Console.WriteLine($"Adapter Failed: Could not find WGPU device {msg}");
                }
            });

        DeviceDescriptor descriptor = new DeviceDescriptor();
        wgpu.AdapterRequestDevice(adapter, descriptor, callback, null);
    }

    private void ConfigureSurface()
    {
        SurfaceConfiguration surfaceConfiguration = new SurfaceConfiguration
        {
            Device = device,
            Width = (uint)window.Size.X,
            Height = (uint)window.Size.Y,
            Format = TextureFormat.Bgra8Unorm,
            PresentMode = PresentMode.Fifo,
            Usage = TextureUsage.RenderAttachment
        };

        wgpu.SurfaceConfigure(surface, surfaceConfiguration);
    }

    private void ConfigureDebugCallback()
    {
        PfnErrorCallback callback = PfnErrorCallback.From(
            (type, msgPtr, userDataPtr) =>
            {
                string msg = Marshal.PtrToStringAnsi((IntPtr)msgPtr);
                Console.WriteLine($"WGPU Unhandled error callback: {msg}");
            });

        wgpu.DeviceSetUncapturedErrorCallback(device, callback, null);
        Console.WriteLine("WGPU Debug Callback Configured");
    }

    public void OnLoad() { }

    public void OnUpdate(double delta) { }

    public void OnRender(double delta)
    {
        BeforeRender();
        // TODO: draw here
        AfterRender();
    }

    private void BeforeRender()
    {
        // Queue
        queue = wgpu.DeviceGetQueue(device);

        // Command Encoder
        currentEncoder = wgpu.DeviceCreateCommandEncoder(device, null);

        // Surface Texture
        wgpu.SurfaceGetCurrentTexture(surface, ref surfaceTexture);
        surfaceTextureView = wgpu.TextureCreateView(surfaceTexture.Texture, null);

        // Render Pass Encoder 
        RenderPassColorAttachment* colorAttachment = stackalloc RenderPassColorAttachment[1];
        colorAttachment[0].View = surfaceTextureView;
        colorAttachment[0].LoadOp = LoadOp.Clear;
        colorAttachment[0].ClearValue = new Color(1, 0.9, 0.9, 1.0);
        colorAttachment[0].StoreOp = StoreOp.Store;

        RenderPassDescriptor renderPassDescriptor = new RenderPassDescriptor
        {
            ColorAttachments = colorAttachment,
            ColorAttachmentCount = 1
        };

        currentRenderPass = wgpu.CommandEncoderBeginRenderPass(currentEncoder, renderPassDescriptor);
    }

    private void AfterRender()
    {
        // End render pass
        wgpu.RenderPassEncoderEnd(currentRenderPass);

        // Finish pipeline
        CommandBuffer* commandBuffer = wgpu.CommandEncoderFinish(currentEncoder, null);

        // Submit command buffer
        wgpu.QueueSubmit(queue, 1, &commandBuffer);

        // Present Surface
        wgpu.SurfacePresent(surface);

        // Dispose Resources
        wgpu.TextureViewRelease(surfaceTextureView);
        wgpu.TextureRelease(surfaceTexture.Texture);
        wgpu.RenderPassEncoderRelease(currentRenderPass);
        wgpu.CommandBufferRelease(commandBuffer);
        wgpu.CommandEncoderRelease(currentEncoder);
    }

    public void Dispose()
    {
        wgpu.DeviceDestroy(device);
        Console.WriteLine("WGPU Device Destroyed");
        wgpu.SurfaceRelease(surface);
        Console.WriteLine("WGPU Surface Released");
        wgpu.AdapterRelease(adapter);
        Console.WriteLine("WGPU Adapter Released");
        wgpu.InstanceRelease(instance);
        Console.WriteLine("WGPU Instance Released");
    }
}
