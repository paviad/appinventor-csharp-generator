using System.Collections.Generic;
using System.Xml.Serialization;

namespace AiaGenerator {
    [XmlRoot(ElementName = "xml", Namespace = "http://www.w3.org/1999/xhtml")]
    public class AiaProgram {
        [XmlElement("block")]
        public List<AiaBlock> Blocks { get; set; }
    }
}
