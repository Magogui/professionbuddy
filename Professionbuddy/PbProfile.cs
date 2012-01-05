//!CompilerOption:Optimize:On
//!CompilerOption:AddRef:WindowsBase.dll

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.IO.Packaging;

using Styx;
using TreeSharp;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.Logic;
using Styx.Logic.Combat;
using System.Diagnostics;
using Styx.Patchables;
using Styx.Plugins;
using Styx.Plugins.PluginClass;
using Styx.Logic.Pathing;
using Styx.Logic.BehaviorTree;
using Styx.WoWInternals.WoWObjects;
using CommonBehaviors.Actions;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Xml;
using Styx.Combat.CombatRoutine;
using Styx.Logic.POI;
using HighVoltz.Composites;

using Action = TreeSharp.Action;
using ObjectManager = Styx.WoWInternals.ObjectManager;
using System.Xml.Linq;
using System.Reflection;
using System.ComponentModel;
using HighVoltz.Dynamic;

namespace HighVoltz
{
    public class PbProfile
    {
        public PbProfile()
        {
            ProfilePath = XmlPath = "";
            //Branch = new PrioritySelector();
        }
        public PbProfile(string path)
        {
            ProfilePath = path;
            LoadFromFile(ProfilePath);
        }
        /// <summary>
        /// Path to a .xml or .package PB profile
        /// </summary>
        public string ProfilePath { get; protected set; }
        /// <summary>
        /// Path to a .xml PB profile
        /// </summary>
        public string XmlPath { get; protected set; }
        /// <summary>
        /// Profile behavior.
        /// </summary>
        //public PrioritySelector Branch { get; protected set; }

        public PbDecorator LoadFromFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    ProfilePath = path;

                    if (Path.GetExtension(path).Equals(".package", StringComparison.InvariantCultureIgnoreCase))
                    {
                        using (Package zipFile = Package.Open(path, FileMode.Open, FileAccess.Read))
                        {
                            var packageRelation = zipFile.GetRelationships().FirstOrDefault();
                            if (packageRelation == null)
                            {
                                Professionbuddy.Err("{0} contains no usable profiles", path);
                                return null;
                            }
                            PackagePart pbProfilePart = zipFile.GetPart(packageRelation.TargetUri);
                            path = ExtractPart(pbProfilePart, DynamicCodeCompiler.TempFolder);
                            var pbProfileRelations = pbProfilePart.GetRelationships();
                            foreach (var rel in pbProfileRelations)
                            {
                                var hbProfilePart = zipFile.GetPart(rel.TargetUri);
                                ExtractPart(hbProfilePart, DynamicCodeCompiler.TempFolder);
                            }
                        }
                    }
                    XmlPath = path;
                    return (PbDecorator)Load(XElement.Load(path), new PbDecorator() as GroupComposite);
                }
                else
                {
                    Professionbuddy.Err("Profile: {0} does not exist", path);
                    return null;
                }
            }
            catch (Exception ex) { Professionbuddy.Err(ex.ToString()); return null; }
        }

        GroupComposite Load(XElement xml, GroupComposite comp)
        {
            foreach (XNode node in xml.Nodes())
            {
                
                if (node.NodeType == XmlNodeType.Comment)
                {
                    comp.AddChild(new Comment(((XComment)node).Value));
                }
                else if (node.NodeType == XmlNodeType.Element)
                {
                    XElement element = (XElement)node;
                    Type type = Type.GetType("HighVoltz.Composites." + element.Name);
                    if (type == null)
                    {
                        IEnumerable<Type> pbTypes = from t in Assembly.GetExecutingAssembly().GetTypes()
                                                    where (typeof(IPBComposite)).IsAssignableFrom(t) && !t.IsAbstract
                                                    select t;
                        type = pbTypes.FirstOrDefault(t => t.GetCustomAttributes(typeof(XmlRootAttribute), true).Any(a => ((XmlRootAttribute)a).ElementName == element.Name));
                        if (pbTypes == null)
                            throw new InvalidOperationException(string.Format("Unable to bind XML Element: {0} to a Type", element.Name));
                    }
                    IPBComposite pbComp = (IPBComposite)Activator.CreateInstance(type);
                    pbComp.OnProfileLoad(element);
                    var pbXmlAttrs = from pi in type.GetProperties()
                                     from attr in (PbXmlAttributeAttribute[])pi.GetCustomAttributes(typeof(PbXmlAttributeAttribute), true)
                                     where attr != null
                                     let name = attr.AttributeName != null ? attr.AttributeName : pi.Name
                                     select new { name, pi };

                    Dictionary<string, PropertyInfo> piDict = pbXmlAttrs.ToDictionary(kv => kv.name, kv => kv.pi);
                    Dictionary<string, string> attributes = element.Attributes().ToDictionary(k => k.Name.ToString(), v => v.Value);
                    // use legacy X,Y,Z location for backwards compatability
                    if (attributes.ContainsKey("X"))
                    {
                        string location = string.Format("{0},{1},{2}", attributes["X"], attributes["Y"], attributes["Z"]);
                        piDict["Location"].SetValue(pbComp, location, null);
                        attributes.Remove("X");
                        attributes.Remove("Y");
                        attributes.Remove("Z");
                    }
                    foreach (KeyValuePair<string, string> attr in attributes)
                    {
                        if (piDict.ContainsKey(attr.Key))
                        {
                            PropertyInfo pi = piDict[attr.Key];
                            // check if there is a type converter attached
                            TypeConverterAttribute typeConverterAttr = (TypeConverterAttribute)pi.GetCustomAttributes(typeof(TypeConverterAttribute), true).FirstOrDefault();
                            if (typeConverterAttr != null)
                            {
                                try
                                {
                                    TypeConverter typeConverter = (TypeConverter)Activator.CreateInstance(Type.GetType(typeConverterAttr.ConverterTypeName));
                                    if (typeConverter.CanConvertFrom(typeof(string)))
                                    {
                                        pi.SetValue(pbComp, typeConverter.ConvertFrom(null, System.Globalization.CultureInfo.CurrentCulture, attr.Value), null);
                                    }
                                    else
                                        Professionbuddy.Err("The TypeConvert {0} can not convert from string.", typeConverterAttr.ConverterTypeName);
                                }
                                catch (Exception ex)
                                {
                                    Professionbuddy.Err("Type conversion for {0} has failed.\n{1}", type.Name + attr.Key, ex);
                                }
                            }
                            else
                            {
                                if (pi.PropertyType.IsEnum)
                                    pi.SetValue(pbComp, Enum.Parse(pi.PropertyType, attr.Value), null);
                                else
                                    pi.SetValue(pbComp, Convert.ChangeType(attr.Value, pi.PropertyType), null);
                            }
                        }
                        else
                            Professionbuddy.Err("Unable to bind xml attribute: {0} to Type: {1}", attr.Key, type);
                    }
                    if (pbComp is GroupComposite)
                        Load(element, pbComp as GroupComposite);
                    comp.AddChild((Composite)pbComp);
                }
            }
            return comp;
        }

        static internal void GetHbprofiles(string pbProfilePath, Composite comp, Dictionary<string, Uri> dict)
        {
            if (comp is LoadProfileAction && !string.IsNullOrEmpty(((LoadProfileAction)comp).Path) &&
                ((LoadProfileAction)comp).ProfileType == LoadProfileAction.LoadProfileType.Honorbuddy)
            {
                Uri profileUri = PackUriHelper.CreatePartUri(new Uri(((LoadProfileAction)comp).Path, UriKind.Relative));
                string pbProfileDirectory = Path.GetDirectoryName(pbProfilePath);
                string profilePath = Path.Combine(pbProfileDirectory, ((LoadProfileAction)comp).Path);
                if (!dict.ContainsKey(profilePath))
                    dict.Add(profilePath, profileUri);
            }
            if (comp is GroupComposite)
            {
                foreach (Composite c in ((GroupComposite)comp).Children)
                {
                    GetHbprofiles(pbProfilePath, c, dict);
                }
            }
        }

        public void SaveXml(string file)
        {
            Save(new XElement("Professionbuddy"), Professionbuddy.Instance.PbBehavior).Save(file);
            XmlPath = file;
        }

        XElement Save(XElement xml, GroupComposite comp)
        {
            foreach (IPBComposite pbComp in comp.Children)
            {
                if (pbComp is Comment)
                {
                    xml.Add(new XComment(((Comment)pbComp).Text));
                }
                else
                {
                    XElement newElement = new XElement(pbComp.GetType().Name);
                    XmlRootAttribute rootAttr = (XmlRootAttribute)pbComp.GetType().GetCustomAttributes(typeof(XmlRootAttribute), true).FirstOrDefault();
                    if (rootAttr != null)
                        newElement.Name = rootAttr.ElementName;
                    pbComp.OnProfileSave(newElement);
                    List<PropertyInfo> piList = pbComp.GetType().GetProperties().
                        Where(p => p.GetCustomAttributes(typeof(PbXmlAttributeAttribute), true).
                        Any()).ToList();
                    foreach (PropertyInfo pi in piList)
                    {
                        List<PbXmlAttributeAttribute> pList = ((PbXmlAttributeAttribute[])pi.GetCustomAttributes(typeof(PbXmlAttributeAttribute), true)).ToList();
                        string name = pList.Any(a => a.AttributeName == null) ? pi.Name : pList[0].AttributeName;
                        string value = "";
                        TypeConverterAttribute typeConverterAttr = (TypeConverterAttribute)pi.GetCustomAttributes(typeof(TypeConverterAttribute), true).FirstOrDefault();
                        if (typeConverterAttr != null)
                        {
                            try
                            {
                                TypeConverter typeConverter = (TypeConverter)Activator.CreateInstance(Type.GetType(typeConverterAttr.ConverterTypeName));
                                if (typeConverter.CanConvertTo(typeof(string)))
                                {
                                    value = (string)typeConverter.ConvertTo(pi.GetValue(pbComp, null), typeof(string));
                                }
                                else
                                    Professionbuddy.Err("The TypeConvert {0} can not convert to string.", typeConverterAttr.ConverterTypeName);
                            }
                            catch (Exception ex)
                            {
                                Professionbuddy.Err("Type conversion for {0}->{1} has failed.\n{2}", comp.GetType().Name, pi.Name, ex);
                            }
                        }
                        else
                        {
                            value = pi.GetValue(pbComp, null).ToString();
                        }
                        newElement.Add(new XAttribute(name, value));
                    }
                    if (pbComp is GroupComposite)
                        Save(newElement, (GroupComposite)pbComp);
                    xml.Add(newElement);
                }
            }
            return xml;
        }

        #region Package
        private const string PackageRelationshipType = @"http://schemas.microsoft.com/opc/2006/sample/document";
        private const string ResourceRelationshipType = @"http://schemas.microsoft.com/opc/2006/sample/required-resource";
        public void CreatePackage(string path, string profilePath)
        {
            try
            {
                Uri partUriProfile = PackUriHelper.CreatePartUri(
                    new Uri(Path.GetFileName(profilePath), UriKind.Relative));
                Dictionary<string, Uri> HbProfileUrls = new Dictionary<string, Uri>();
                GetHbprofiles(profilePath, Professionbuddy.Instance.PbBehavior, HbProfileUrls);
                using (Package package = Package.Open(path, FileMode.Create))
                {
                    // Add the PB profile
                    PackagePart packagePartDocument =
                        package.CreatePart(partUriProfile, System.Net.Mime.MediaTypeNames.Text.Xml, CompressionOption.Normal);
                    using (FileStream fileStream = new FileStream(
                           profilePath, FileMode.Open, FileAccess.Read))
                    {
                        CopyStream(fileStream, packagePartDocument.GetStream());
                    }
                    package.CreateRelationship(packagePartDocument.Uri, TargetMode.Internal, PackageRelationshipType);

                    foreach (var kv in HbProfileUrls)
                    {
                        PackagePart packagePartHbProfile =
                            package.CreatePart(kv.Value, System.Net.Mime.MediaTypeNames.Text.Xml, CompressionOption.Normal);

                        using (FileStream fileStream = new FileStream(kv.Key, FileMode.Open, FileAccess.Read))
                        {
                            CopyStream(fileStream, packagePartHbProfile.GetStream());
                        }
                        packagePartDocument.CreateRelationship(kv.Value, TargetMode.Internal, ResourceRelationshipType);
                    }
                }
            }
            catch (Exception ex)
            { Professionbuddy.Err(ex.ToString()); }
        }

        void CopyStream(Stream source, Stream target)
        {
            const int bufSize = 0x1000;
            byte[] buf = new byte[bufSize];
            int bytesRead = 0;
            while ((bytesRead = source.Read(buf, 0, bufSize)) > 0)
                target.Write(buf, 0, bytesRead);
        }

        private string ExtractPart(PackagePart packagePart, string targetDirectory)
        {
            string packageRelative = Uri.UnescapeDataString(packagePart.Uri.ToString().TrimStart('/'));
            string fullPath = Path.Combine(targetDirectory, packageRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (FileStream fileStream = new FileStream(fullPath, FileMode.Create))
            {
                CopyStream(packagePart.GetStream(), fileStream);
            }
            return fullPath;
        }
        #endregion
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
    public class PbXmlAttributeAttribute : Attribute
    {
        public PbXmlAttributeAttribute()
        {
        }

        public PbXmlAttributeAttribute(string attributeName)
        {
            this.AttributeName = attributeName;
        }

        public string AttributeName { get; private set; }
    }
}
