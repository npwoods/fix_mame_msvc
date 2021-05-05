using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace FixMameMsvc
{
	class Program
	{
		private static XmlNamespaceManager _nsMgr = CreateNamespaceManager();

		private static XmlNamespaceManager CreateNamespaceManager()
		{
			var result = new XmlNamespaceManager(new NameTable());
			result.AddNamespace("", "http://schemas.microsoft.com/developer/msbuild/2003");
			result.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003");
			return result;
		}

		private static bool ApplyFix(XmlDocument xmlDoc, string xPath, string childNode, string newInnerText)
		{
			bool changed = false;
			foreach(XmlNode node in xmlDoc.SelectNodes(xPath, _nsMgr))
			{
				XmlNode nodeToChange;
				if (childNode != null)
				{
					nodeToChange = null;
					foreach (XmlNode child in node.ChildNodes)
					{
						if (child.Name == childNode)
						{
							nodeToChange = child;
							break;
						}
					}

					if (nodeToChange == null)
					{ 
						nodeToChange = xmlDoc.CreateElement("", childNode, "http://schemas.microsoft.com/developer/msbuild/2003");
						node.AppendChild(nodeToChange);
					}
				}
				else
				{
					nodeToChange = node;
				}

				nodeToChange.InnerText = newInnerText;
				changed = true;
			}
			return changed;
		}

		private static void FixProject(string fileName)
		{
			var xmlDoc = new XmlDocument(_nsMgr.NameTable);
			xmlDoc.PreserveWhitespace = true;
			xmlDoc.Load(fileName);

			bool changed = false;

			if (ApplyFix(xmlDoc, "/ms:Project/ms:PropertyGroup/ms:WindowsTargetPlatformVersion", null, "10.0.17763.0"))
				changed = true;
			if (ApplyFix(xmlDoc, "/ms:Project/ms:PropertyGroup/ms:PlatformToolset", null, "ClangCL"))
				changed = true;
			if (ApplyFix(xmlDoc, "/ms:Project/ms:ItemDefinitionGroup[@Condition]/ms:ClCompile/ms:TreatWarningAsError", null, "false"))
				changed = true;
			if (ApplyFix(xmlDoc, "/ms:Project/ms:ItemDefinitionGroup[@Condition]/ms:ClCompile", "LanguageStandard", "stdcpp17"))
				changed = true;

			// Special case for portaudio.vcxproj
			if (Path.GetFileName(fileName) == "portaudio.vcxproj")
			{
				foreach (XmlNode node in xmlDoc.SelectNodes("/ms:Project/ms:ItemDefinitionGroup/ms:Lib/ms:AdditionalDependencies", _nsMgr))
				{
					string innerText = node.InnerText;
					if (innerText.StartsWith("ksuser.lib"))
					{
						innerText = innerText.Replace("ksuser.lib;", "");
						node.InnerText = innerText;
						changed = true;
					}
				}
			}

			// Spercial case for EXEs
			foreach(XmlNode linkNode in xmlDoc.SelectNodes("/ms:Project/ms:ItemDefinitionGroup[@Condition]/ms:Link", _nsMgr))
			{
				if (ApplyFix(xmlDoc, "/ms:Project/ms:ItemGroup/ms:Image/DeploymentContent", null, "false"))
					changed = true;

				if (linkNode.SelectSingleNode("./ms:SubSystem", _nsMgr)?.InnerText == "Console")
				{
					var debugNode = linkNode.SelectSingleNode("./ms:GenerateDebugInformation", _nsMgr);
					if (debugNode != null)
					{
						debugNode.InnerText = "DebugFull";
						changed = true;
					}

					var additionalDepsNode = linkNode.SelectSingleNode("./ms:AdditionalDependencies", _nsMgr);
					if (additionalDepsNode != null && !additionalDepsNode.InnerText.Contains("ksuser.lib"))
					{
						additionalDepsNode.InnerText = "ksuser.lib;" + additionalDepsNode.InnerText;
						changed = true;
					}
				}
			}

			if (changed)
			{
				Console.WriteLine($"Fixed {fileName}");
				xmlDoc.Save(fileName);
			}
		}

		private static void FixDirectory(string directory)
		{
			foreach(var file in Directory.GetFiles(directory, "*.vcxproj"))
			{
				FixProject(file);
			}

			foreach(var subDirectory in Directory.GetDirectories(directory))
			{
				FixDirectory(subDirectory);
			}
		}

		static void Main(string[] args)
		{
			FixDirectory(args[0]);
		}
	}
}
