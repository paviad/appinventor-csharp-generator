using System;
using System.Xml;
using System.Xml.Serialization;

namespace AiaGenerator {
    public class AiaBlock {
        [XmlAnyElement]
        public XmlElement[] XmlElements { get; set; } = Array.Empty<XmlElement>();

        [XmlAnyAttribute]
        public XmlAttribute[] XmlAttributes { get; set; } = Array.Empty<XmlAttribute>();

        [XmlAttribute("id")]
        public string Id { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlElement("field")]
        public AiaField[] Fields { get; set; } = Array.Empty<AiaField>();


        [XmlElement("value")]
        public AiaBlockContainer[] Values { get; set; } = Array.Empty<AiaBlockContainer>();

        [XmlElement("statement")]
        public AiaBlockContainer[] Statements { get; set; } = Array.Empty<AiaBlockContainer>();

        [XmlElement("next")]
        public AiaBlockContainer[] Nexts { get; set; } = Array.Empty<AiaBlockContainer>();

        [XmlElement("mutation")]
        public AiaMutation Mutation { get; set; }
    }

    public class AiaMutation {
        [XmlAttribute("else")]
        public string Else1 { get; set; }

        [XmlIgnore]
        public int Else => string.IsNullOrEmpty(Else1) ? 0 : int.Parse(Else1);

        [XmlAttribute("items")]
        public string Items1 { get; set; }

        [XmlIgnore]
        public int? Items => string.IsNullOrEmpty(Items1) ? (int?)null : int.Parse(Items1);

        [XmlElement("localname")]
        public AiaLocalname[] AiaLocalnames { get; set; } = Array.Empty<AiaLocalname>();

        [XmlElement("arg")]
        public AiaArg[] AiaArgs { get; set; } = Array.Empty<AiaArg>();
    }

    public class AiaLocalname {
        [XmlAttribute("name")]
        public string Name { get; set; }
    }

    public class AiaArg {
        [XmlAttribute("name")]
        public string Name { get; set; }
    }

    public class AiaBlockContainer {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("block")]
        public AiaBlock Block { get; set; }
    }

    public class AiaField {
        [XmlAnyElement]
        public XmlElement[] XmlElements { get; set; } = Array.Empty<XmlElement>();

        [XmlAnyAttribute]
        public XmlAttribute[] XmlAttributes { get; set; } = Array.Empty<XmlAttribute>();

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
}
