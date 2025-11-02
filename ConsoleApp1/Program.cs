using ConsoleApp1;
using ConsoleApp1.Pipelines;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            
            using Engine engine = new Engine();
            
            UnlitRenderPipeline unlitRenderPipeline = new UnlitRenderPipeline(engine);

            engine.OnInitialize += () =>
            {
                unlitRenderPipeline.Initialize();
            };

            engine.OnRender += () =>
            {
                unlitRenderPipeline.Render();
            };
            
            engine.Initialize(); // window.Run() blocks here
            
        }
    }
}



