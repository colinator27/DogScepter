// Project for testing random portions of code as features are developed

using DogScepterLib.Project;
using DogScepterLib.Project.GML.Compiler;

ProjectFile pf = DogScepterTest.Util.BasicLoadProject("data.win", null, Console.WriteLine);

var ctx = new CompileContext(pf);
ctx.AddCode("Custom_Script",
@"
if (get_integer(""Enter a truthy or falsey integer"", 1))
    show_message(""Truthy value was entered"");
else
    show_message(""Falsey value was entered"");
variable_global_set(""__TEST_GLOBAL__"", ""Test global value"")
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

return;