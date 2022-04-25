// Project for testing random portions of code as features are developed

using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project;
using DogScepterLib.Project.GML.Compiler;
using DogScepterLib.Project.GML.Decompiler;

ProjectFile pf = DogScepterTest.Util.BasicLoadProject("data.win", null, Console.WriteLine);

#if false

GMUniquePointerList<GMCode> codeList = pf.DataHandle.GetChunk<GMChunkCODE>().List;
foreach (var elem in codeList)
{
    if (elem.ParentEntry != null)
        continue;
    if (elem.Name.Content.Contains("persist_load"))
        new DecompileContext(pf).DecompileWholeEntryString(elem);
}

#else

var ctx = new CompileContext(pf);
ctx.AddCode("Custom_Script",
@"
enum TEST
{
    A = 123,
    B
}

//repeat (10)
{
    //with (obj_player)
    {
        switch (x)
        {
            case 0:
                return TEST.A;
            case 1:
                return TEST.B;
        }
    }
}

colortest = #ff0000

b = self
with (other)
    self.b = 123 + TEST.B
a = 1
for (var i = 0; i < 10; i = i + 1)
{
    a = a + i
}
show_message(string(i));
var b;

if (get_integer(""Enter a truthy or falsey integer"", 1))
    show_message(""Truthy value was entered"");
else
    show_message(""Falsey value was entered"");
variable_global_set(""__TEST_GLOBAL__"", ""Test global value"");
show_message(global.__TEST_GLOBAL__);

//obj_fader.func(""Test string"");
//obj_fader.abc.func(""Test"");
//other.a().func(""Test"");
//abc.func(1, 2, 3);
//abc(10).func(1, 2, 3);
//abc(10).abc(20, 21).func(1, 2, 3);
//abc(10).def.gjh.func(1, 2, 3);
//abc(10).def[0].gjh.func(1, 2, 3);
//abc(10).def[789][987].gjh.func(1, 2, 3);
//abc(10)[789][987].gjh.func(1, 2, 3);
//abc(10)().gjh.func(1, 2, 3);
", CodeContext.CodeMode.Replace, true);
Console.WriteLine(ctx.Compile() ? "Compile success" : "Compile failure");

DogScepterTest.Util.BasicSaveProject(pf, "data_modded.win", Console.WriteLine);

#endif

return;