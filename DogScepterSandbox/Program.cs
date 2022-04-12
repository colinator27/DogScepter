// Project for testing random portions of code as features are developed

using DogScepterLib.Project;
using DogScepterLib.Project.GML.Compiler;

ProjectFile pf = DogScepterTest.Util.BasicLoadProject("data.win", null, Console.WriteLine);

var ctx = new CompileContext(pf);
ctx.AddCode("gml_GlobalScript_Custom_Script",
@"
show_message(""Test message!"");

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