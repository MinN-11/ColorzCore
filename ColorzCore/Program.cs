﻿using System;
using System.IO;
using ColorzCore.IO;
using ColorzCore.DataTypes;

namespace ColorzCore
{
    class Program
    {
        public static bool Debug = false;
        private static string[] helpstringarr = {"EA Colorz Core. Usage:",
            "./ColorzCore <A|D|AA> <game> [-opts]",
            "",
            "A is to write ROM directly",
            "AA is to output assembly source file and linker script",
            "D is not allowed currently.",
            "Game may be any string; the respective _game_ variable gets defined in scripts.",
            "Available options:",
            "-raws:<dir>",
            "   Sets the raws directory to the one provided (relative to ColorzCore). Defaults to \"Language Raws\".",
            "-rawsExt:<ext>",
            "   Sets the extension of files used for raws to the one provided. Defaults to .txt.",
            "-output:<filename>",
            "   Set the file to write assembly to.",
            "-input:<filename>",
            "   Set the file to take input script from. Defaults to stdin.",
            "-error:<filename>",
            "   Set a file to redirect messages, warnings, and errors to. Defaults to stderr.",
            "-werr",
            "   Treat all warnings as errors and prevent assembly.",
            "--no-mess",
            "   Suppress output of messages.",
            "--no-warn",
            "   Suppress output of warnings.",
            "--quiet",
            "   Equivalent to --no-mess --no-warn.",
            "-h|--help",
            "   Display helpstring and exit.",
            "-debug",
            "   Enable debug mode. Not recommended for end users."};
        private static string helpstring = System.Linq.Enumerable.Aggregate(helpstringarr, (String a, String b) => { return a + '\n' + b; }) + '\n';

        private const int EXIT_SUCCESS = 0;
        private const int EXIT_FAILURE = 1;

        static int Main(string[] args)
        {
            EAOptions options = new EAOptions();

            IncludeFileSearcher rawSearcher = new IncludeFileSearcher();
            rawSearcher.IncludeDirectories.Add(AppDomain.CurrentDomain.BaseDirectory);

            Stream inStream = Console.OpenStandardInput();
            string inFileName = "stdin";

            IOutput output = null;
            string outFileName = "none";
            string ldsFileName = "none";

            TextWriter errorStream = Console.Error;

            Maybe<string> rawsFolder = rawSearcher.FindDirectory("Language Raws");
            string rawsExtension = ".txt";

            if (args.Length < 2)
            {
                Console.WriteLine("Required parameters missing.");
                return EXIT_FAILURE;
            }

            bool outputASM = false;
            if (args[0] == "AA")
                outputASM = true;
            else
                if (args[0] != "A")
            {
                Console.WriteLine("Only assembly is supported currently.");
                return EXIT_FAILURE;
            }

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i][0] != '-')
                {
                    Console.Error.WriteLine("Unrecognized paramter: " + args[i]);
                }
                else
                {
                    string[] flag = args[i].Substring(1).Split(new char[] { ':' }, 2);

                    try
                    {
                        switch (flag[0])
                        {
                            case "raws":
                                rawsFolder = rawSearcher.FindDirectory(flag[1]);
                                break;

                            case "rawsExt":
                                rawsExtension = flag[1];
                                break;

                            case "output":
                                outFileName = flag[1];
                                if(outputASM)
                                {
                                    ldsFileName = Path.ChangeExtension(outFileName, "lds");
                                    output = new ASM(new StreamWriter(outFileName, false),
                                                     new StreamWriter(ldsFileName, false));
                                } else
                                {
                                    output = new ROM(File.Open(outFileName, FileMode.Open, FileAccess.ReadWrite)); //TODO: Handle file not found exceptions
                                } 
                                break;

                            case "input":
                                inFileName = flag[1];
                                inStream = File.OpenRead(flag[1]);
                                break;

                            case "error":
                                errorStream = new StreamWriter(File.OpenWrite(flag[1]));
                                options.noColoredLog = true;
                                break;

                            case "debug":
                                Debug = true;
                                break;

                            case "werr":
                                options.werr = true;
                                break;

                            case "-no-mess":
                                options.nomess = true;
                                break;

                            case "-no-warn":
                                options.nowarn = true;
                                break;

                            case "-no-colored-log":
                                options.noColoredLog = true;
                                break;

                            case "quiet":
                                options.nomess = true;
                                options.nowarn = true;
                                break;

                            case "-nocash-sym":
                                options.nocashSym = true;
                                break;

                            case "I":
                            case "-include":
                                options.includePaths.Add(flag[1]);
                                break;

                            case "T":
                            case "-tools":
                                options.toolsPaths.Add(flag[1]);
                                break;

                            case "IT":
                            case "TI":
                                options.includePaths.Add(flag[1]);
                                options.toolsPaths.Add(flag[1]);
                                break;

                            case "h":
                            case "-help":
                                Console.Out.WriteLine(helpstring);
                                return EXIT_SUCCESS;

                            default:
                                Console.Error.WriteLine("Unrecognized flag: " + flag[0]);
                                return EXIT_FAILURE;
                        }
                    }
                    catch (IOException e)
                    {
                        Console.Error.WriteLine("Exception: " + e.Message);
                        return EXIT_FAILURE;
                    }
                }
            }
            
            if (output == null)
            {
                Console.Error.WriteLine("No output specified for assembly.");
                return EXIT_FAILURE;
            }

            if (rawsFolder.IsNothing)
            {
                Console.Error.WriteLine("Couldn't find raws folder");
                return EXIT_FAILURE;
            }

            string game = args[1];

            //FirstPass(Tokenizer.Tokenize(inputStream));

            Log log = new Log {
                Output = errorStream,
                WarningsAreErrors = options.werr,
                NoColoredTags = options.noColoredLog
            };

            if (options.nowarn)
                log.IgnoredKinds.Add(Log.MsgKind.WARNING);

            if (options.nomess)
                log.IgnoredKinds.Add(Log.MsgKind.MESSAGE);

            EAInterpreter myInterpreter = new EAInterpreter(output, game, rawsFolder.FromJust, rawsExtension, inStream, inFileName, log, options);

            bool success = myInterpreter.Interpret();

            if (success && options.nocashSym)
            {
                using (var symOut = File.CreateText(Path.ChangeExtension(outFileName, "sym")))
                {
                    if (!(success = myInterpreter.WriteNocashSymbols(symOut)))
                    {
                        log.Message(Log.MsgKind.ERROR, "Error trying to write no$gba symbol file.");
                    }
                }
            }

            inStream.Close();
            output.Close();
            errorStream.Close();

            return success ? EXIT_SUCCESS : EXIT_FAILURE;
        }
    }
}
