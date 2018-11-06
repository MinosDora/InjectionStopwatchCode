using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            FileStream fileStream = new FileStream(args[i], FileMode.Open);
            if (fileStream != null)
            {
                AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(fileStream);
                ModuleDefinition moduleDefinition = assemblyDefinition.MainModule;
                Collection<TypeDefinition> typeDefinition = moduleDefinition.Types;
                foreach (TypeDefinition type in typeDefinition)
                {
                    if (type.IsClass)
                    {
                        foreach (MethodDefinition method in type.Methods)
                        {
                            if (method.IsPublic && !method.IsConstructor)
                            {
                                ILProcessor iLProcessor = method.Body.GetILProcessor();
                                TypeReference stopWatchType = moduleDefinition.ImportReference(typeof(Stopwatch));
                                VariableDefinition variableDefinition = new VariableDefinition(stopWatchType);
                                method.Body.Variables.Add(variableDefinition);
                                Instruction firstInstruction = method.Body.Instructions.First();
                                iLProcessor.InsertBefore(firstInstruction, iLProcessor.Create(OpCodes.Newobj, moduleDefinition.ImportReference(typeof(Stopwatch).GetConstructor(new Type[] { }))));
                                iLProcessor.InsertBefore(firstInstruction, iLProcessor.Create(OpCodes.Stloc_S, variableDefinition));
                                iLProcessor.InsertBefore(firstInstruction, iLProcessor.Create(OpCodes.Ldloc_S, variableDefinition));
                                iLProcessor.InsertBefore(firstInstruction, iLProcessor.Create(OpCodes.Callvirt, moduleDefinition.ImportReference(typeof(Stopwatch).GetMethod("Start"))));
                                Instruction[] returnInstructions = method.Body.Instructions.Where(item => item.OpCode == OpCodes.Ret).ToArray();  //因为不能在foreach循环中修改Instructions的值，所以这里使用数组缓存并遍历修改
                                for (int j = 0; j < returnInstructions.Length; j++)
                                {
                                    Instruction returnInstruction = returnInstructions[j];
                                    iLProcessor.InsertBefore(returnInstruction, iLProcessor.Create(OpCodes.Ldloc_S, variableDefinition));
                                    iLProcessor.InsertBefore(returnInstruction, iLProcessor.Create(OpCodes.Callvirt, moduleDefinition.ImportReference(typeof(Stopwatch).GetMethod("Stop"))));

                                    iLProcessor.InsertBefore(returnInstruction, iLProcessor.Create(OpCodes.Ldstr, $"{method.FullName} run time: "));
                                    iLProcessor.InsertBefore(returnInstruction, iLProcessor.Create(OpCodes.Ldloc_S, variableDefinition));
                                    iLProcessor.InsertBefore(returnInstruction, iLProcessor.Create(OpCodes.Callvirt, moduleDefinition.ImportReference(typeof(Stopwatch).GetMethod("get_ElapsedMilliseconds"))));
                                    iLProcessor.InsertBefore(returnInstruction, iLProcessor.Create(OpCodes.Box, moduleDefinition.ImportReference(typeof(long))));
                                    iLProcessor.InsertBefore(returnInstruction, iLProcessor.Create(OpCodes.Call, moduleDefinition.ImportReference(typeof(string).GetMethod("Concat", new Type[] { typeof(object), typeof(object) }))));
                                    iLProcessor.InsertBefore(returnInstruction, iLProcessor.Create(OpCodes.Call, moduleDefinition.ImportReference(typeof(Console).GetMethod("WriteLine", new Type[] { typeof(string) }))));
                                }
                            }
                        }
                    }
                }
                FileInfo fileInfo = new FileInfo(args[i]);
                string fileName = fileInfo.Name;
                int pointIndex = fileName.LastIndexOf('.');
                string frontName = fileName.Substring(0, pointIndex);
                string backName = fileName.Substring(pointIndex, fileName.Length - pointIndex);
                string writeFilePath = Path.Combine(fileInfo.Directory.FullName, frontName + "_inject" + backName);
                assemblyDefinition.Write(writeFilePath);
                Console.WriteLine($"Success! Output path: {writeFilePath}");
                fileStream.Dispose();
            }
        }
        Console.Read();
    }
}