﻿using NBTExplorer.Model;
using NBTUtil.Ops;
using System;
using System.Collections.Generic;

namespace NBTUtil
{
    internal class ConsoleRunner
    {
        private static readonly Dictionary<ConsoleCommand, ConsoleOperation> _commandTable =
            new Dictionary<ConsoleCommand, ConsoleOperation>
            {
                {ConsoleCommand.SetValue, new EditOperation()},
                {ConsoleCommand.DeleteValue, new DeleteOperation()},
                {ConsoleCommand.SetList, new SetListOperation()},
                {ConsoleCommand.Print, new PrintOperation()},
                {ConsoleCommand.PrintTree, new PrintTreeOperation()},
                {ConsoleCommand.Json, new JsonOperation()}
            };

        private readonly ConsoleOptions _options;

        public ConsoleRunner()
        {
            _options = new ConsoleOptions();
        }

        public bool Run(string[] args)
        {
            // Parse and validate command line arguments.

            _options.Parse(args);

            if (_options.Command == ConsoleCommand.Help)
                return PrintHelp();

            if (_options.Path == null)
                return PrintUsage("Error: You must specify a path");
            if (!_commandTable.ContainsKey(_options.Command))
                return PrintUsage("Error: No command specified");

            var op = _commandTable[_options.Command];
            if (!op.OptionsValid(_options))
                return PrintError("Error: Invalid options specified for the given command");

            var successCount = 0;
            var failCount = 0;

            var nodesToProcess = new List<DataNode>();

            // Iterate over all nodes matching the provided Path and create a list of the ones that can be processed
            // using the provided ConsoleCommand.

            foreach (var node in new NbtPathEnumerator(_options.Path))
            {
                if (op.CanProcess(node))
                {
                    nodesToProcess.Add(node);
                }
                else
                {
                    Console.WriteLine(node.NodePath + ": ERROR (invalid command)");
                    failCount++;
                }
            }

            // Iterate over all the processable nodes and process them.
            // Doing this separately from the CanProcess loop allows Process to make significant changes to the NBT
            // tree like node deletion.

            foreach (var targetNode in nodesToProcess) {
                // Since Process may render targetNode inoperable, save targetNode.Root beforehand.
                var root = targetNode.Root;

                if (op.Process(targetNode, _options))
                {
                    // Now that processing has succeeded, save the changes.
                    root.Save();
                    Console.WriteLine(targetNode.NodePath + ": OK");
                    successCount++;
                }
                else
                {
                    // Since processing failed, discard any changes that may have been made. This prevents other
                    // iterations of this loop from saving them.
                    targetNode.RefreshNode();
                    Console.WriteLine(targetNode.NodePath + ": ERROR (apply)");
                    failCount++;
                }
            }

            Console.WriteLine("Operation complete.  Nodes succeeded: {0}  Nodes failed: {1}", successCount, failCount);

            return true;
        }

        private DataNode OpenFile(string path)
        {
            DataNode node = null;
            foreach (var item in FileTypeRegistry.RegisteredTypes)
                if (item.Value.NamePatternTest(path))
                    node = item.Value.NodeCreate(path);

            return node;
        }

        private DataNode ExpandDataNode(DataNode dataNode, string tagPath)
        {
            var pathParts = tagPath.Split('/');

            var curTag = dataNode;
            curTag.Expand();

            foreach (var part in pathParts)
            {
                var container = curTag as TagDataNode.Container;
                if (curTag == null)
                    throw new Exception("Invalid tag path");

                DataNode childTag = null;
                foreach (var child in curTag.Nodes)
                    if (child.NodePathName == part)
                        childTag = child;

                if (childTag == null)
                    throw new Exception("Invalid tag path");

                curTag.Expand();
            }

            return curTag;
        }

        private bool PrintHelp()
        {
            Console.WriteLine("NBTUtil - Copyright 2014 Justin Aquadro");
            _options.PrintUsage();

            return true;
        }

        private bool PrintUsage(string error)
        {
            Console.WriteLine(error);
            _options.PrintUsage();

            return false;
        }

        private bool PrintError(string error)
        {
            Console.WriteLine(error);

            return false;
        }
    }
}