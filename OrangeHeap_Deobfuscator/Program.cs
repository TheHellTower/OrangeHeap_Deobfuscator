using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OrangeHeap_Deobfuscator
{
    internal class Program
    {
        static ModuleDefMD Module = null;
        static FileInfo FI = null;
        static List<MethodDef> methodsToClean = new List<MethodDef>();
        static bool launchedFromCMD = Console.Title.Contains($" - {Assembly.GetEntryAssembly().FullName.Split(',')[0]}");

        static void CRL()
        {
            if(!launchedFromCMD)
                Console.ReadLine();
        }

        static void Main(string[] args)
        {
            Module = ModuleDefMD.Load(args[0]);
            FI = new FileInfo(Module.Location);
            decryptStrings(Module);
            clean(Module);
            Module.Write(Module.Location.Replace(FI.Extension, $"-orangeheap{FI.Extension}"));
            CRL();
        }

        private static void decryptStrings(ModuleDefMD module)
        {
            foreach (TypeDef type in module.Types.Where(t => t.HasMethods))
            {
                MethodDef stringsMethodPerType = type.Methods.Where(m => m.HasBody && m.Body.HasInstructions && m.IsPrivate && m.ReturnType.ToString().Equals("System.String") && m.Parameters.Count() == 1 && m.Parameters.First().Type.ToString().Equals("System.String")).First();
                methodsToClean.Add(stringsMethodPerType);
                foreach (MethodDef method in type.Methods.Where(m => m.HasBody && m.Body.HasInstructions))
                    foreach (Instruction i in method.Body.Instructions.ToArray())
                        if (i.OpCode == OpCodes.Call && i.Operand.ToString().Contains($"{stringsMethodPerType.Name}(System.String)"))
                        {
                            int index = method.Body.Instructions.IndexOf(i);
                            Instruction encryptedString = method.Body.Instructions[index-1];
                            string decryptedString = decryptString(encryptedString.Operand.ToString());
                            encryptedString.Operand = decryptedString;
                            Console.WriteLine($"Decrypted: {decryptedString}");
                            method.Body.Instructions.RemoveAt(index);
                        }
            }
        }

        private static void clean(ModuleDefMD module)
        {
            foreach(MethodDef useless in methodsToClean)
            {
                Console.WriteLine($"Removed: {useless.Name}");
                useless.DeclaringType.Remove(useless);
                try
                {
                    module.Types.Remove(module.Types.Where(t => t.Name == "OrangeHeapAttribute").First());
                } catch
                {
                    //Console.WriteLine("Already deleted.");
                }
            }
        }

        private static string decryptString(string encryptedString)
        {
            int length = encryptedString.Length;
            char[] array = new char[length];
            for (int i = 0; i < array.Length; i++)
            {
                char c = encryptedString[i];
                array[i] = (char)(((int)(byte)((int)(c >> 8) ^ i) << 8) | (int)(byte)((int)c ^ (length - i)));
            }
            return string.Intern(new string(array));
        }
    }
}