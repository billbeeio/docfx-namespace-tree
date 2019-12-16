using System;
using System.Reflection.Metadata;

namespace MyExampleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(MyExampleNestedNs.testClass.helloWorld);
        }

    }

    namespace MyExampleNestedNs
    {
        static class testClass
        {
            public static string helloWorld = "Hello World!";
        }
    }
}