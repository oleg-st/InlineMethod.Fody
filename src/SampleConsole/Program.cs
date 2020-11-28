using System;

namespace SampleConsole
{
    unsafe class Program
    {
        [InlineMethod.Inline]
        public static void Test(int x, int y, int z)
        {
            z = 88;
            int xx = 33;
            int yy = 3;
            Console.WriteLine($"Test {x} {y} {z + xx}");
            Test2((ulong)y);
            xx++;

            if (x > y)
            {
                Console.WriteLine("gt");
            }
            else
            {
                Console.WriteLine("lt");
            }
        }

        [InlineMethod.Inline]
        public static void Test2(ulong a)
        {
            Test3();
            Console.WriteLine("Test2 1");
            Console.WriteLine($"Test2 2 {a}");
            Test3();
        }

        [InlineMethod.Inline]
        public static int Test3()
        {
            return 55;
        }

        public static void A(int x, int y)
        {
            B(x + 1, y);
            B(x + 1, y + 2);

            MEM_writeLE16(&x, 0);
        }

        [InlineMethod.Inline]
        private static void MEM_writeLE16(void* memPtr, ushort val)
        {
            *(ushort*)memPtr = val;
        }

        [InlineMethod.Inline]
        public static void B(int x, int y)
        {
            switch (x)
            {
                case 1:
                    Console.WriteLine(1);
                    break;
                case 2:
                    Console.WriteLine(2);
                    break;
                case 3:
                    Console.WriteLine(3);
                    break;
                case 4:
                    Console.WriteLine(4);
                    break;
            }

            C(x, y);
        }

        public static void C(int x, int y)
        {
            Console.WriteLine(x);
        }

        public int Rec(int x)
        {
            return x > 0 ? Rec(x - 1) : 0;
        }

        static void Main(string[] args)
        {
            ulong x = 5;
            ushort y = 55;
            MEM_writeLE16(&x, y);

            Console.WriteLine($"Main 1 {x}");

            int t1 = 2, t2 = 3, t3 = 4, t4 = 5;

            int test = 5;
            Test(2, 2 + test + Test3(), 3 + test);
            if (test > 3 && t3 > 3)
            {
            }

            Console.WriteLine($"Main 2 {test}");

            Console.WriteLine("Main 3");

            new P().Test2(5);
        }
    }

    class P
    {
        [InlineMethod.Inline]
        public void Test1(int x, int y)
        {
            Console.WriteLine($"Test1 {x} {y}");
            Test2(x + 1, y + 1);
        }

        [InlineMethod.Inline]
        public void Test2(int x, int y)
        {
            Console.WriteLine($"Test2 {x} {y}");
        }

        [InlineMethod.Inline]
        public void Test2(int c)
        {
            var t = this;

            Console.WriteLine("Test2 1");
            Test1(1 ,2);
            Console.WriteLine("Test2 2");
        }
    }
}
