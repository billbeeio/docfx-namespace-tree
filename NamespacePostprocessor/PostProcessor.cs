
using System;
using Microsoft.DocAsCode.Plugins;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using SharpYaml.Serialization;
using System.Collections.Generic;
using System.Diagnostics;

namespace Billbee.DocFx
{
    [Export(nameof(NamespacePostProcessor), typeof(IPostProcessor))]
    public class NamespacePostProcessor : IPostProcessor
    {
        ImmutableDictionary<string, object> IPostProcessor.PrepareMetadata(ImmutableDictionary<string, object> metadata)
        {
            Debug.WriteLine($"###### DocFx NamespacePostProcessor Start ######");
            try
            {
                IEnumerable<string> yamlFilePaths = getManagedReferenceFiles();
                if (yamlFilePaths == null)
                    return metadata;

                IList<string> createdFiles = new List<string>();
                
                foreach (var yamlFilePath in yamlFilePaths)
                {
                    var sourceFilePath = yamlFilePath;
                    var targetFilePaths = getPaths(sourceFilePath);
                    if (targetFilePaths == null)
                        continue;

                    foreach (var targetFilePath in targetFilePaths)
                    {
                        if (!updateTargetFile(sourceFilePath, targetFilePath))
                        {
                            sourceFilePath = targetFilePath;
                            continue; // nothing to do
                        }
    
                        if (!createdFiles.Contains(targetFilePath))
                            createdFiles.Add(targetFilePath);

                        Debug.WriteLine($"nested namespaces postprocessor: Added/Updated file '{targetFilePath}'");
                        
                        sourceFilePath = targetFilePath;
                    }
                }

                // ATTENTION: When changing 'yamlFiles' or 'yamlFileSourceDir'
                //            here, you'll need to update DocumentBuilder.cs in DocFx source code, too.
                metadata = metadata.Add("yamlFiles", createdFiles);
                metadata = metadata.Add("yamlFileSourceDir", "./intermediate_files/metadata/");
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw;
            }
            Debug.WriteLine($"###### DocFx NamespacePostProcessor End ######");

            return metadata;
        }

        /*
         * NOTE: The code below basically works.
         * Unluckily, when executed, all the html files already have been
         * generated from the yml files. Therefore, it makes no sense
         * to add or modify yml files at this point.
         * Luckily, I noticed that yml files are generated already when
         * IPostprocessor.PrepareMetadata is called while html files are
         * not generated at that point. Perfect :-)
         */

        Manifest IPostProcessor.Process(Manifest manifest, string outputFolder)
        {

            /*
                        if (manifest == null)
                            return manifest;

                        try {
                            var files = manifest.Files.OrderBy(file => file.SourceRelativePath);
                            var workingDirectory = Environment.CurrentDirectory;
                            List<ManifestItem> newItems = new List<ManifestItem>();

                            foreach (var file in files)
                            {
                                if (file.DocumentType != "ManagedReference")
                                    continue;

                                var sourceFilePath = workingDirectory + "\\" + file.SourceRelativePath;

                                var targetFilePaths = getPaths(sourceFilePath);
                                if (targetFilePaths == null)
                                    continue;

                                foreach (var targetFilePath in targetFilePaths)
                                {
                                    bool fileExisted = File.Exists(targetFilePath);
                                    bool changes = updateTargetFile(sourceFilePath, targetFilePath);

                                    if (changes || !fileExisted)
                                    {
                                        int index = (workingDirectory + "intermediate_files/metadata/").Length + 1;
                                        var relativePath = targetFilePath.Substring(index);
                                        relativePath = relativePath.Substring(0, relativePath.LastIndexOf('.'));
                                        newItems.Add(createManifestItem(relativePath));
                                    }

                                    sourceFilePath = targetFilePath;
                                }
                            }
                            manifest.Files.AddRange(newItems);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                            throw;
                        }
            */
            return manifest;
        }

        /// <summary>
        /// Gets all yaml file pathes (full path) generated by docfx
        /// containing information about managed references.
        /// Returned list is ordered by file name.
        /// </summary>
        private IEnumerable<string> getManagedReferenceFiles()
        {
            var resourceFileDirectory = Environment.CurrentDirectory + "\\intermediate_files\\metadata\\resources\\";
            IEnumerable<string> fileNames = Directory.EnumerateFiles(resourceFileDirectory);
            fileNames = fileNames.Where(fileName => fileName.EndsWith(".yml"));

            List<string> referenceFiles = new List<string>();
            foreach (var fileName in fileNames)
            {
                string firstLine = File.ReadLines(fileName)?.First();
                if (!firstLine?.StartsWith("### YamlMime:ManagedReference") ?? true)
                    continue;

                referenceFiles.Add(fileName);
            }

            return referenceFiles.OrderBy(file => file);
        }

        /// <summary>
        /// Creates or updates a target yaml file containing a namespace
        /// to contain all references to the source yaml file.
        /// </summary>
        private bool updateTargetFile(string sourceFilePath, string targetFilePath)
        {
            if (sourceFilePath == null || targetFilePath == null)
                return false;

            var targetYaml = loadYaml(targetFilePath, false);
            if (targetYaml != null && getTypeOfFirstItem(targetYaml) != "Namespace")
                return false; // target file contains no namespace

            // load source file
            var sourceYaml = loadYaml(sourceFilePath, false);
            if (getTypeOfFirstItem(sourceYaml) != "Namespace")
                return false; // source file contains no namespace

            if (targetYaml == null)
                targetYaml = loadYaml(targetFilePath, true);

            using (FileStream targetStream =
                new FileStream(targetFilePath, FileMode.OpenOrCreate, FileAccess.Write))
            {
                bool changes = getTargetYaml(sourceYaml, ref targetYaml);

                if (!changes)
                    return false;

                using (var streamWriter = new StreamWriter(targetStream, Encoding.UTF8))
                {
                    streamWriter.AutoFlush = true;
                    targetYaml?.Save(streamWriter, true);
                    streamWriter.Flush();
                }
            }
            return true;
        }

        /// <summary>
        /// Creates or updates a target yaml stream containing a namespace
        /// to contain all references to the source yaml stream.
        /// </summary>
        private bool getTargetYaml(YamlStream sourceYaml, ref YamlStream targetYaml)
        {
            if (targetYaml == null)
                targetYaml = new YamlStream();

            var sourceDoc = sourceYaml?.Documents?.FirstOrDefault();
            if (sourceDoc == null)
                return false;

            var targetDoc = targetYaml.Documents?.FirstOrDefault();
            if (targetDoc == null)
            {
                targetDoc = createTargetDoc(sourceDoc);
                targetYaml.Add(targetDoc);
            }
            // yaml comments are not imported. Therefore, we need to set it everytime we update the file...
            (targetDoc.RootNode as YamlMappingNode).Tag = "### YamlMime:ManagedReference";

            return addSourceInfo(sourceDoc, targetDoc);
        }

        private static YamlStream loadYaml(string path, bool createIfNotExists)
        {
            if (!File.Exists(path))
                return createIfNotExists ? new YamlStream() : null;

            var yaml = new YamlStream();

            using (var streamReader = new StreamReader(path))
            {
                yaml.Load(streamReader);
            }

            return yaml;
        }

        /// <summary>
        /// Creates file names for files to be created/updated from the given file.
        /// For example, for file C:\MyNamespace.MySubNamespace.MyClass.yml,
        /// generates C:\MyNamespace.MySubNamespace.yml and C:\MyNamespace.yml
        /// </summary>
        private string[] getPaths(string fullPathToFile)
        {
            var path = fullPathToFile;
            if (path == null)
                return null;

            var fileNameStartIndex = path.LastIndexOfAny(new char[] {'\\', '/'}) + 1;
            var fileTypeStartIndex = path.LastIndexOf('.') + 1;
            if (fileNameStartIndex < 0
                || fileNameStartIndex >= fileTypeStartIndex
                || fileTypeStartIndex >= path.Length)
                return null;

            var basePath = path.Substring(0, fileNameStartIndex);
            var fileName = path.Substring(fileNameStartIndex, path.Length - fileNameStartIndex);
            var splitFileName = fileName.Split('.');
            if (splitFileName.Length < 3)
                return null;

            var fileType = splitFileName.Last();

            string[] pathes = new string[splitFileName.Length - 2];

            string currentFileName = "";
            for (int i = 0; i < pathes.Length; i++)
            {
                currentFileName += splitFileName[i] + '.';
                pathes[(pathes.Length-1) - i] = basePath + currentFileName + fileType;
            }

            return pathes;
        }

        /// <summary>
        /// Creates a yml file for the namespace containing the
        /// item(s) of the source document.
        /// If source document contains MyNameSpace.Utilities.MyClass,
        /// created file will contain namespace MyNameSpace.Utilities
        /// </summary>
        /// <param name="sourceDoc"></param>
        /// <returns></returns>
        private YamlDocument createTargetDoc(YamlDocument sourceDoc)
        {
            var root = new YamlMappingNode();
            root.Tag = "### YamlMime:ManagedReference";

            var items = new YamlSequenceNode();
            root.Add("items", items);
            
            var references = new YamlSequenceNode();
            root.Add("references", references);

            string name = getUidOfFirstItem(sourceDoc, true);
            var namespaceInfo = createItem(name, "Namespace");
            items?.Add(namespaceInfo);

            return new YamlDocument(root);
        }

        /// <summary>
        /// Creates an item that can be inserted into the 'items' section of a managed reference yml file.
        /// </summary>
        private static YamlMappingNode createItem(string fullName, string itemType)
        {
            var namespaceInfo = new YamlMappingNode();

            namespaceInfo.Add("uid", fullName);
            namespaceInfo.Add("commentId", "N:" + fullName);
            namespaceInfo.Add("id", fullName);
            namespaceInfo.Add("name", fullName);
            namespaceInfo.Add("nameWithType", fullName);
            namespaceInfo.Add("fullName", fullName);
            namespaceInfo.Add("type", itemType);
            var lastDotIndex = fullName.LastIndexOf('.');
            if (lastDotIndex > 0) // root namespace has no parent...
                namespaceInfo.Add("parent", fullName?.Substring(0, lastDotIndex));
            return namespaceInfo;
        }

        /// <summary>
        /// Copies all sequence items of the source sequence to the sequence of the target item, if not added before.
        /// </summary>
        private bool copySequence(YamlMappingNode sourceItem, YamlMappingNode targetItem, string sequenceName)
        {
            var sourceSequence = getSequence(sourceItem, sequenceName, false);
            if (sourceSequence?.Children == null)
                return false;

            var targetSequence = getSequence(targetItem, sequenceName, true);
            if (targetSequence == null)
                return false;

            bool changes = false;

            foreach (var sequenceItem in sourceSequence.Children)
            {
                var sourceSequenceItem = (sequenceItem as YamlScalarNode);
                if (targetSequence.Children == null || !targetSequence.Children.Contains(sourceSequenceItem))
                {
                    targetSequence.Add(sourceSequenceItem?.Value);
                    changes = true;
                }
            }

            return changes;
        }

        /// <summary>
        /// Adds information from source doc to target doc.
        /// After calling this, the target doc contains all necessary references to the source item
        /// and also updates its information to contain source items information, such as the 'langs' section.
        /// </summary>
        private bool addSourceInfo(YamlDocument sourceDoc, YamlDocument targetDoc)
        {
            var sourceRoot = sourceDoc?.RootNode as YamlMappingNode;
            var targetItem = getItem(targetDoc, 0);
            YamlSequenceNode sourceItems = sourceRoot?.Children?[new YamlScalarNode("items")] as YamlSequenceNode;
            if (sourceItems?.Children == null)
                return false;

            if (sourceItems.Children.Count > 1)
            {
                string name = getUidOfFirstItem(sourceDoc, false);
                Debug.WriteLine($"Found several items in document with ithem {name}");
            }

            bool changes = false;
            foreach (var item in sourceItems.Children)
            {
                var sourceItem = item as YamlMappingNode;
                if (sourceItem == null)
                    continue;

                bool isNamespace = (sourceItem?.Children?[new YamlScalarNode("type")] as YamlScalarNode).Value == "Namespace";
                if (!isNamespace)
                    continue;
                
                changes |= addSourceToChildren(sourceItem, targetItem);
                changes |= copySequence(sourceItem, targetItem, "langs");
                changes |= addReference(targetDoc, sourceItem);
            }

            return changes;
        }

        /// <summary>
        /// Adds a reference from the target item's 'children' section to the source item,
        /// if reference doesn't exist yet
        /// </summary>
        /// <param name="sourceItem"></param>
        /// <param name="targetItem"></param>
        /// <returns></returns>
        private static bool addSourceToChildren(YamlMappingNode sourceItem, YamlMappingNode targetItem)
        {
            YamlScalarNode childUid = (sourceItem?.Children?[new YamlScalarNode("uid")] as YamlScalarNode);
            YamlSequenceNode existingChildren = getSequence(targetItem, "children", true);
            if (existingChildren == null)
                return false;
            if (existingChildren.Children != null && existingChildren.Children.Contains(childUid))
                return false;

            existingChildren.Add(childUid?.Value);
            return true;
        }

        /// <summary>
        /// Adds a reference to the source item to the target document's 'references' section
        /// if reference doesn't exist yet.
        /// </summary>
        private bool addReference(YamlDocument targetDoc, YamlMappingNode sourceItem)
        {
            if (sourceItem?.Children == null)
                return false;

            var sourceUid = (sourceItem.Children[new YamlScalarNode("uid")] as YamlScalarNode)?.Value;

            var targetRoot = targetDoc?.RootNode as YamlMappingNode;
            YamlSequenceNode references = getSequence(targetRoot, "references", true);
            
            if (references?.Children != null)
            {
                foreach (var r in references.Children)
                {
                    var existingRef = r as YamlMappingNode;

                    string uid = (existingRef?.Children?[new YamlScalarNode("uid")] as YamlScalarNode)?.Value;
                    if (uid == sourceUid)
                        return false; // reference exists already
                }
            }

            var reference = new YamlMappingNode();

            var sourceCommentId = (sourceItem.Children[new YamlScalarNode("commentId")] as YamlScalarNode)?.Value;
            var sourceName = (sourceItem.Children[new YamlScalarNode("name")] as YamlScalarNode)?.Value;
            var sourceFullName = (sourceItem.Children[new YamlScalarNode("fullName")] as YamlScalarNode)?.Value;
            var sourceNameWithType = (sourceItem.Children[new YamlScalarNode("nameWithType")] as YamlScalarNode)?.Value;
            
            reference.Add("uid", sourceUid);
            reference.Add("commentId", sourceCommentId);
            reference.Add("name", sourceName);
            reference.Add("nameWithType", sourceNameWithType);
            reference.Add("fullName", sourceFullName);

            var lastDotIndex = sourceUid.LastIndexOf('.');
            if (lastDotIndex > 0) // root namespace has no parent...
                reference.Add("parent", sourceUid.Substring(0, lastDotIndex));

            references.Add(reference);
            return true;
        }

        /// <summary>
        /// Gets the uid of the first item from a managed references yml files 'items' section.
        /// </summary>
        /// <param name="removeLastPathItem">If true, returns 'MyNamespace.MySubNamespace
        /// for MyNamespace.MySubNamespace.MyClass</param>
        /// <returns></returns>
        private string getUidOfFirstItem(YamlDocument document, bool removeLastPathItem)
        {
            var firstItem = getItem(document, 0);

            string uid = (firstItem?.Children?[new YamlScalarNode("uid")] as YamlScalarNode)?.Value;

            if (uid != null && removeLastPathItem)
                uid = uid.Substring(0, uid.LastIndexOf('.'));

            return uid;
        }

        /// <summary>
        /// Gets the type of the first item from a managed references yml files 'items' section.
        /// Could be e.g. 'Namespace', 'Class', 'Enum' etc.
        /// </summary>
        private string getTypeOfFirstItem(YamlStream stream)
        {
            var document = stream?.Documents?.FirstOrDefault();
            if (document == null)
                return "";

            var firstItem = getItem(document, 0);

            return (firstItem?.Children?[new YamlScalarNode("type")] as YamlScalarNode)?.Value ?? "";
        }

        /// <summary>
        /// gets an item from the 'items' list at top of yml file
        /// </summary>
        private static YamlMappingNode getItem(YamlDocument document, int index)
        {
            var root = document?.RootNode as YamlMappingNode;
            YamlSequenceNode items = root?.Children?[new YamlScalarNode("items")] as YamlSequenceNode;

            return items?.Children?[index] as YamlMappingNode;
        }

        /// <summary>
        /// Gets a sequence (that is, a list), with the given name from the parent node.
        /// </summary>
        /// <param name="create">Creates and inserts sequence into parent node if it doesn't exist.</param>
        /// <returns></returns>
        private static YamlSequenceNode getSequence(YamlMappingNode parentNode, string sequenceName, bool create)
        {
            YamlSequenceNode sequence = null;
            var nodeKey = new YamlScalarNode(sequenceName);
            if (parentNode?.Children?.ContainsKey(nodeKey) ?? false)
                sequence = parentNode?.Children?[nodeKey] as YamlSequenceNode;

            if (sequence == null && create)
            {
                sequence = new YamlSequenceNode();
                parentNode?.Add(sequenceName, sequence);
            }

            return sequence;
        }
    }
}
