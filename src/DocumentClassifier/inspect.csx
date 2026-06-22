using System;
using System.Reflection;
using System.Linq;

var asm = Assembly.LoadFrom(@"C:\Users\cwoodland\.nuget\packages\microsoft.agents.ai.workflows\1.10.0\lib\net9.0\Microsoft.Agents.AI.Workflows.dll");
var wbt = asm.GetType("Microsoft.Agents.AI.Workflows.WorkflowBuilder");
if (wbt == null) { Console.WriteLine("Type not found"); return; }
foreach (var m in wbt.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == "AddEdge"))
{
    var ps = m.GetParameters();
    var pstr = string.Join(", ", ps.Select(p => p.ParameterType.Name + " " + p.Name));
    var gen = m.IsGenericMethod ? $"<{string.Join(",", m.GetGenericArguments().Select(g => g.Name))}>" : "";
    Console.WriteLine($"  AddEdge{gen}({pstr})");
}
