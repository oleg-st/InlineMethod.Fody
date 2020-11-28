# InlineMethod.Fody

This is an add-in for [Fody](https://github.com/Fody/Fody) which lets you inline methods in compile time.

## Example

What you write:

```C#
static unsafe int Read32(void* memPtr) => *(ushort*) memPtr;

[InlineMethod.Inline]
static unsafe int Read32WithInline(void* memPtr) => *(ushort*)memPtr;

public void Test()
{
    ulong x = 5;
    int y = Read32(&x);
}

public void TestWithInline()
{
    ulong x = 5;
    int y = Read32WithInline(&x);
}
```

What gets compiled:

```
  .method private hidebysig static int32 
          Read32(void* memPtr) cil managed
  {
    // Code size       3 (0x3)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldind.u2
    IL_0002:  ret
  } // end of method Program::Read32

  .method public hidebysig instance void 
          Test() cil managed
  {
    // Code size       13 (0xd)
    .maxstack  1
    .locals init (uint64 V_0)
    IL_0000:  ldc.i4.5
    IL_0001:  conv.i8
    IL_0002:  stloc.0
    IL_0003:  ldloca.s   V_0
    IL_0005:  conv.u
    IL_0006:  call       int32 SampleConsole.Program::Read32(void*)
    IL_000b:  pop
    IL_000c:  ret
  } // end of method Program::Test

  .method public hidebysig instance void 
          TestWithInline() cil managed
  {
    // Code size       9 (0x9)
    .maxstack  1
    .locals init (uint64 V_0)
    IL_0000:  ldc.i4.5
    IL_0001:  conv.i8
    IL_0002:  stloc.0
    IL_0003:  ldloca.s   V_0
    IL_0005:  conv.u
    IL_0006:  ldind.u2
    IL_0007:  pop
    IL_0008:  ret
  } // end of method Program::TestWithInline

```
