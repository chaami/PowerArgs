﻿using System;
namespace HelloWorld
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {

            // Samples.SearchSample.Run();
            // Samples.ProgressBarSample.Run();
            // Samples.CalculatorProgramSample._Main(args); // a simple 4 function calculator
            //Samples.HelloWorldParse._Main(args);            //  The simplest way to use the parser.  All this sample does is parse the arguments and send them back to your program.
            // Samples.HelloWorldInvoke._Main(args);        //  A simple way to have the parser parse your arguments and then call a new Main method that you build.
            // Samples.Git._Main(args);                     //  Sample that shows how to implement a program that accepts multiple commands and where each command takes its own set of arguments.
             Samples.REPLInvoke._Main(args);              //  Sample that shows how to implement a REPL (Read Evaluate Print Loop)
            // Samples.HelloWorldConditionalIInvoke._Main(args);
        }


    }
}                                  
 