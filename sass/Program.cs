﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Globalization;

namespace sass
{
    public class Program
    {
		static bool asmsx = false;

        public static Dictionary<string, InstructionSet> InstructionSets;

        public static int Main(string[] args)
        {
            InstructionSets = new Dictionary<string, InstructionSet>();
			InstructionSets.Add("z80", LoadInternalSet("sass.Tables.z80.table"));
            InstructionSets.Add("z80alt", LoadInternalSet("sass.Tables.z80alt.table"));
            string instructionSet = "z80"; // Default
            string inputFile = null, outputFile = null;
            var settings = new AssemblySettings();
            List<string> defines = new List<string>();

			Console.WriteLine ("-------------------------------------------------------------------------------");
			Console.WriteLine ("sassMSX v.0.1 WIP cross-assembler. KnightOS [2015], Libertium Games[2016/07/26]");
			Console.WriteLine ("-------------------------------------------------------------------------------");
			// Assembling labels, calls and jumps
			// Output text file game.txt saved
			// Binary file game.rom saved
			// Symbol file game.sym saved
			// Completed in 1.07 seconds

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("-") && arg != "-")
                {
                    try
                    {
                        switch (arg)
                        {
                            case "-d":
                            case "--define":
                                defines.AddRange(args[++i].Split(','));
                                break;
                            case "--debug-mode":
                                Thread.Sleep(10000);
                                break;
                            case "--encoding":
                                try
                                {
                                    settings.Encoding = Encoding.GetEncoding(args[++i]);
                                }
                                catch
                                {
                                    Console.Error.WriteLine("The specified encoding was not recognized. Use sass --list-encodings to see available encodings.");
                                    return 1;
                                }
                                break;
                            case "-h":
                            case "-?":
                            case "/?":
                            case "/help":
                            case "-help":
                            case "--help":
                                DisplayHelp();
                                return 0;
                            case "--inc":
                            case "--include":
                                settings.IncludePath = args[++i].Split(';');
                                break;
                            case "--input":
                            case "--input-file":
                                inputFile = args[++i];
                                break;
                            case "--instr":
                            case "--instruction-set":
                                instructionSet = args[++i];
                                break;
							case "-as":
							case "--asmsx":
								instructionSet = "z80alt";
								asmsx = true;
								break;
						
                            case "-l":
                            case "--listing":
								if( !args[i+1].StartsWith("-"))
                                	settings.ListingOutput = args[++i];
								else
									Console.WriteLine("!!--listing whithout parameter !!");
							
                                break;
                            case "--list-encodings":
                                Console.WriteLine("The default encoding is UTF-8. The following are available: ");
                                foreach (var encoding in Encoding.GetEncodings())
                                    Console.WriteLine("{0} [{1}]", encoding.DisplayName, encoding.Name);
                                Console.WriteLine("Use the identifier (in [brackets]) with --encoding).");
                                return 0;
                            case "--nest-macros":
                                settings.AllowNestedMacros = true;
                                break;
                            case "--output":
                            case "--output-file":
                                outputFile = args[++i];
                                break;
                            case "-s":
                            case "--symbols":
                                settings.SymbolOutput = args[++i];
                                break;
                            case "-v":
                            case "--verbose":
                                settings.Verbose = true;
								Console.WriteLine("Verbose:");
                                break;
                        }
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        Console.Error.WriteLine("Error: Invalid usage. Use sass.exe --help for usage information.");
                        return 1;
                    }
                }
                else
                {
                    if (inputFile == null)
                        inputFile = args[i];
                    else if (outputFile == null)
                        outputFile = args[i];
                    else
                    {
                        Console.Error.WriteLine("Error: Invalid usage. Use sass.exe --help for usage information.");
                        return 1;
                    }
                }
            }

            if (inputFile == null)
            {
				Console.Error.WriteLine ("Syntax: sassMSX [file.asm]");
                Console.Error.WriteLine("No input file specified. Use sass.exe --help for usage information.");
                return 1;
            }
            if (outputFile == null)
                outputFile = Path.GetFileNameWithoutExtension(inputFile) + ".bin";

            InstructionSet selectedInstructionSet;
            if (!InstructionSets.ContainsKey(instructionSet))
            {
                if (File.Exists(instructionSet))
                    selectedInstructionSet = InstructionSet.Load(File.ReadAllText(instructionSet));
                else
                {
                    Console.Error.WriteLine("Specified instruction set was not found.");
                    return 1;
                }
            }
            else
                selectedInstructionSet = InstructionSets[instructionSet];

            var assembler = new Assembler(selectedInstructionSet, settings);

			assembler.ASMSX = asmsx;

            foreach (var define in defines)
                assembler.ExpressionEngine.Symbols.Add(define.ToLower(), new Symbol(1));
            string file ="";
			if (inputFile == "-")
				file = Console.In.ReadToEnd ();
			else if (File.Exists (inputFile)) {
				file = File.ReadAllText (inputFile);
			} else {
				Console.Error.WriteLine("File not found: {0}",inputFile);
				Console.Error.WriteLine ("Press any key to continue...");
				Console.ReadKey (true);
			}
            var watch = new Stopwatch();
            
			AssemblyOutput output = null;

			if (file != "") {
				watch.Start ();
				output = assembler.Assemble (file, inputFile);
			
				settings.Verbose = settings.Verbose | assembler.EnableVerbose;

				var errors = from l in output.Listing
						where l.Warning != AssemblyWarning.None || l.Error != AssemblyError.None
					orderby l.RootLineNumber
					select l;
				if (!settings.Verbose) {
					foreach (var listing in errors) {
						if (listing.Error != AssemblyError.None)
							Console.Error.WriteLine (listing.FileName + " " + listing.Error + " " + listing.LineNumber + " " + listing.Code);
					}
				}

				if (settings.Verbose || settings.ListingOutput != null) {
					var listing = GenerateListing (output);
					if (settings.Verbose)
						Console.Write (listing);
					if (settings.ListingOutput != null)
					{
						File.WriteAllText (settings.ListingOutput, listing);
						Console.WriteLine ("Output text file " + settings.ListingOutput + " saved");
					}
				}


				// Cesc: todo pendent de validar amb casos de prova i en especial les ROM de 48KB
				if (assembler.IsROM) {
					
					// Sizes: 16KB to 48KB in steps of 16KB
					int pages = 2;	// TODO Default 32KB
					int size = pages * 16 * 1024;	
					byte[] rom = setROMHeader (size, assembler.ROMStart);
				
					int srcAdress = 0;
					foreach (Assembler.ORGItem item in assembler.ORGsList) {
						uint orgIni;
						if (srcAdress == 0)
							orgIni = item.ORGAdress - 0x4000 + 16;
						else
							orgIni = item.ORGAdress - 0x4000;
							
						if (item.ORGAdress < 0xc000) {
							for (uint i = orgIni; i < (item.ORGLength - 0x4000); i++) {
								rom [i] = output.Data [srcAdress];
								srcAdress++;
							}
						}
					}
					File.WriteAllBytes (outputFile.Replace (".bin", ".rom"), rom);
				} 
				else if (assembler.IsMegaROM) 
				{
					// Sizes: 128KB to 512KB in steps of 8KB
					int pages =0;	

					foreach (Assembler.ORGItem item in assembler.ORGsList) {
						pages = (int)Math.Max (pages, item.Subpage);

					}
					pages++;
					int size = pages * 8 * 1024;	
					byte[] rom = setROMHeader (size, assembler.ROMStart);

					int srcAdress = 0;
					uint subpage =0;
					uint SubpageORG =0;

					foreach (Assembler.ORGItem item in assembler.ORGsList) {
						uint orgIni;
						if (item.Subpage != subpage) {
							subpage = item.Subpage;
							SubpageORG = item.ORGAdress;
						}

						if (srcAdress == 0)
						{
							orgIni = 16;
							subpage = item.Subpage;
							SubpageORG = item.ORGAdress;
						}
						else
							orgIni = (item.ORGAdress - SubpageORG) + item.Subpage *0x2000;

						if (item.ORGAdress < 0xc000) {
							uint len = (item.ORGAdress - SubpageORG) + item.Subpage *0x2000 + (item.ORGLength - item.ORGAdress);
							for (uint i = orgIni; i < len; i++) {
								rom [i] = output.Data [srcAdress];
								srcAdress++;
							}
						}
					}

					if (errors.Count (e => e.Error != AssemblyError.None) == 0) {
						File.WriteAllBytes (outputFile.Replace (".bin", ".rom"), rom);
						Console.WriteLine ("Binary file " + outputFile.Replace (".bin", ".rom") + " saved");

						File.WriteAllBytes (outputFile, output.Data);
						Console.WriteLine ("Binary file " + outputFile + " saved");
					} else {
						Console.WriteLine ("Errors count:{0}",errors.Count(e => e.Error != AssemblyError.None));
					}
				}
				else
				{
					if (outputFile == "-")
						Console.OpenStandardOutput ().Write (output.Data, 0, output.Data.Length);
					else
						File.WriteAllBytes (outputFile, output.Data);
				}
					
				if (settings.SymbolOutput != null)
					WriteSymbols (settings.SymbolOutput, assembler);

				watch.Stop ();
				Console.Error.WriteLine ("Assembly done: {0:F2} s", watch.ElapsedMilliseconds/1000d);
				if (Debugger.IsAttached) {
					Console.Error.WriteLine ("Press any key to continue...");
					Console.ReadKey (true);
				}
				return errors.Count (e => e.Error != AssemblyError.None);
			}
			return -1;
        }

		/// <summary>
		/// Sets the ROM header:
		///	.db "AB"             ; ID bytes
		///	.dw initmain       	 ; cartridge initialization pointer
		///	.dw 0                ; statement handler (not used)
		///	.dw 0                ; device handler (not used)
		///	.dw 0                ; BASIC program in ROM (not used, especially not in page 1)
		///	.dw 0,0,0            ; reserved
		/// </summary>
		/// <param name="rom">Rom.</param>
		/// <param name="ROMStart">ROM start.</param>
		private static byte[] setROMHeader(int size, uint ROMStart)
		{
			byte[] rom = new byte[size]; //{ (byte)'A', (byte)'B' };
			rom [0] = (byte)'A';
			rom [1] = (byte)'B';
			var romStart = BitConverter.GetBytes (ROMStart);
			rom [2] = romStart.Take (2).ToArray () [0];
			rom [3] = romStart.Take (2).ToArray () [1];
			for (int i = 4; i < 16; i++)
				rom [i] = 0;
			return rom;
		}

        private static void WriteSymbols(string path, Assembler assembler)
        {
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("; This file was generated by sass");
                foreach (var symbol in assembler.ExpressionEngine.Symbols)
                {
                    if (symbol.Value.IsLabel && !symbol.Key.Contains("@")) // The latter removes globalized local labels
                        writer.WriteLine(string.Format(".equ {0} 0x{1}", symbol.Key, symbol.Value.Value.ToString("X")));
                }
            }
        }

		/// <summary>
		/// Generates the listing.
		/// Listing format looks something like this:
		/// file.asm/1 (0x1234): DE AD BE EF    ld a, 0xBEEF
		/// file.asm/2 (0x1236):              label:
		/// file.asm/3 (0x1236):              #directive
		/// </summary>
		/// <returns>The listing.</returns>
		/// <param name="output">Output.</param>
        public static string GenerateListing(AssemblyOutput output)
        {
            // I know this can be optimized, I might optmize it eventually
            int maxLineNumber = output.Listing.Max(l => l.CodeType == CodeType.Directive ? 0 : l.LineNumber).ToString().Length;
            int maxFileLength = output.Listing.Max(l => l.FileName.Length);
            int maxBinaryLength = output.Listing.Max(l =>
                {
                    if (l.Output == null || l.Output.Length == 0)
                        return 0;
                    return l.Output.Length * 3 - 1;
                });
			maxBinaryLength = 8;
            int addressLength = output.InstructionSet.WordSize / 4 + 2;
            string formatString = "{0,-" + maxFileLength + "}:{1,-" + maxLineNumber + "} ({2}): {3,-" + maxBinaryLength + "}  {4}" + Environment.NewLine;
            string errorFormatString = "{0,-" + maxFileLength + "}:{1,-" + maxLineNumber + "} {2}: {3}" + Environment.NewLine;
            string addressFormatString = "X" + addressLength;

			var builder = new StringBuilder();
            string file, address, binary, code;
            int line;
            foreach (var entry in output.Listing)
            {
                file = entry.FileName;
                line = entry.LineNumber;
                address = "0x" + entry.Address.ToString(addressFormatString);
                code = entry.Code;
                if (entry.Output != null && entry.Output.Length != 0 && entry.CodeType != CodeType.Directive)
                {
                    binary = string.Empty;
                    for (int i = 0; i < entry.Output.Length; i++)
                        binary += entry.Output[i].ToString("X2") + " ";
                    binary = binary.Remove(binary.Length - 1);
                    code = "  " + code;
                }
                else
                    binary = string.Empty;
                if (entry.Error != AssemblyError.None)
                    builder.AppendFormat(errorFormatString, file, line, "Error", entry.Error);
                if (entry.Warning != AssemblyWarning.None)
                    builder.AppendFormat(errorFormatString, file, line, "Warning", entry.Warning);
                builder.AppendFormat(formatString, file, line, address, binary, code);
            }
            return builder.ToString();
        }

        public static Stream LoadResource(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        }

        public static InstructionSet LoadInternalSet(string name)
        {
            InstructionSet set;
            using (var stream = new StreamReader(LoadResource(name)))
                set = InstructionSet.Load(stream.ReadToEnd());
            return set;
        }

        private static void DisplayHelp()
        {
            // TODO
			Console.WriteLine("-h -? /? /help -help --help: \tDisplayHelp();");
			Console.WriteLine("-d --define: \t\t\tdefines.AddRange(args[++i].Split(','));");
			Console.WriteLine("--debug-mode: \t\t\ttThread.Sleep(10000);");
			Console.WriteLine("--encoding: \t\t\tsettings.Encoding = Encoding.GetEncoding(args[++i]);");
			Console.WriteLine("--inc --include: \t\ttsettings.IncludePath = args[++i].Split(';');");
			Console.WriteLine("--input --input-file: \t\ttinputFile = args[++i];");
			Console.WriteLine("--instr --instruction-set: \ttinstructionSet = args[++i];");
			Console.WriteLine("-l --listing: \t\t\tsettings.ListingOutput = args[++i];");
			Console.WriteLine("--list-encodings: \t\ttEncoding.GetEncodings()");
			Console.WriteLine("--nest-macros: \t\t\tsettings.AllowNestedMacros = true;");
			Console.WriteLine("--output: --output-file: \toutputFile = args[++i];");
			Console.WriteLine("-s --symbols: \t\t\tsettings.SymbolOutput = args[++i];");
			Console.WriteLine("-v --verbose: \t\t\tsettings.Verbose = true;");
			Console.WriteLine("-as --asmsx: \t\tasMSX syntax");
        }    
	}
}
