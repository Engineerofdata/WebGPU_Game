using ConsoleApp1;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            
            using Engine engine = new Engine();
            engine.Initialize(); // window.Run() blocks here
            
        }
    }
}



