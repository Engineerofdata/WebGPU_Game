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
    
    private Instance* instance = null;
    private Surface* surface = null;
    private Adapter* adapter = null;
    

    // Pipeline queue
    private Queue* queue = null;
    private CommandEncoder* currentEncoder = null;
    private SurfaceTexture surfaceTexture;
    private TextureView* surfaceTextureView = null;

    public event Action OnInitialize;

    public event Action OnRender;
    
    //Components of pipeline
    public WebGPU WGPU { get; private set; }
    public Device* DEVICE { get; private set; }

    public TextureFormat perferredTexture => TextureFormat.Bgra8Unorm;
    
    public RenderPassEncoder* CurrentRenderPassEncoder { get; private set; }
    
    
    
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
        window.Render += WindowOnRender;
        
        OnInitialize?.Invoke();

        window.Run();
    }

    private void CreateApi()
    {
        WGPU = WebGPU.GetApi();
        Console.WriteLine("WGPU API : It loaded");
    }

    private void CreateInstance()
    {
        InstanceDescriptor descriptor = new InstanceDescriptor();
        instance = WGPU.CreateInstance(descriptor);
        Console.WriteLine("WGPU Instance Created");
    }

    private void CreateSurface()
    {
        surface = window.CreateWebGPUSurface(WGPU, instance);
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

        WGPU.InstanceRequestAdapter(instance, options, callback, null);
    }

    private void CreateDevice()
    {
        PfnRequestDeviceCallback callback = PfnRequestDeviceCallback.From(
            (status, wgpuDevice, msgPtr, userDataPtr) =>
            {
                if (status == RequestDeviceStatus.Success)
                {
                    this.DEVICE = wgpuDevice;
                    Console.WriteLine("Device Created");
                }
                else
                {
                    string msg = Marshal.PtrToStringAnsi((IntPtr)msgPtr);
                    Console.WriteLine($"Adapter Failed: Could not find WGPU device {msg}");
                }
            });

        DeviceDescriptor descriptor = new DeviceDescriptor();
        WGPU.AdapterRequestDevice(adapter, descriptor, callback, null);
    }

    private void ConfigureSurface()
    {
        SurfaceConfiguration surfaceConfiguration = new SurfaceConfiguration
        {
            Device = DEVICE,
            Width = (uint)window.Size.X,
            Height = (uint)window.Size.Y,
            Format = perferredTexture,
            PresentMode = PresentMode.Fifo,
            Usage = TextureUsage.RenderAttachment
        };

        WGPU.SurfaceConfigure(surface, surfaceConfiguration);
    }

    private void ConfigureDebugCallback()
    {
        PfnErrorCallback callback = PfnErrorCallback.From(
            (type, msgPtr, userDataPtr) =>
            {
                string msg = Marshal.PtrToStringAnsi((IntPtr)msgPtr);
                Console.WriteLine($"WGPU Unhandled error callback: {msg}");
            });

        WGPU.DeviceSetUncapturedErrorCallback(DEVICE, callback, null);
        Console.WriteLine("WGPU Debug Callback Configured");
    }

    public void OnLoad() { }

    public void OnUpdate(double delta) { }

    public void WindowOnRender(double delta)
    {
        BeforeRender();
        
        OnRender?.Invoke();
        
        AfterRender();
    }

    private void BeforeRender()
    {
        // Queue
        queue = WGPU.DeviceGetQueue(DEVICE);

        // Command Encoder
        currentEncoder = WGPU.DeviceCreateCommandEncoder(DEVICE, null);

        // Surface Texture
        WGPU.SurfaceGetCurrentTexture(surface, ref surfaceTexture);
        surfaceTextureView = WGPU.TextureCreateView(surfaceTexture.Texture, null);

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

        CurrentRenderPassEncoder = WGPU.CommandEncoderBeginRenderPass(currentEncoder, renderPassDescriptor);
    }

    private void AfterRender()
    {
        // End render pass
        WGPU.RenderPassEncoderEnd(CurrentRenderPassEncoder);

        // Finish pipeline
        CommandBuffer* commandBuffer = WGPU.CommandEncoderFinish(currentEncoder, null);

        // Submit command buffer
        WGPU.QueueSubmit(queue, 1, &commandBuffer);

        // Present Surface
        WGPU.SurfacePresent(surface);

        // Dispose Resources
        WGPU.TextureViewRelease(surfaceTextureView);
        WGPU.TextureRelease(surfaceTexture.Texture);
        WGPU.RenderPassEncoderRelease(CurrentRenderPassEncoder);
        WGPU.CommandBufferRelease(commandBuffer);
        WGPU.CommandEncoderRelease(currentEncoder);
    }

    public void Dispose()
    {
        WGPU.DeviceDestroy(DEVICE);
        Console.WriteLine("WGPU Device Destroyed");
        WGPU.SurfaceRelease(surface);
        Console.WriteLine("WGPU Surface Released");
        WGPU.AdapterRelease(adapter);
        Console.WriteLine("WGPU Adapter Released");
        WGPU.InstanceRelease(instance);
        Console.WriteLine("WGPU Instance Released");
    }
}
