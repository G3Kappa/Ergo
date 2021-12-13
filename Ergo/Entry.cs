﻿using Ergo.Lang;
using System;
using System.IO;

namespace Ergo
{
    class Entry
    {
        static void Main(string[] args)
        {
            var interpreter = new Interpreter();
            var shell = new Shell(interpreter);
            shell.EnterRepl();
        }
    }
}
