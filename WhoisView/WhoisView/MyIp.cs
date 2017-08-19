using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace WhoisView
{
    [DataContract]
    public class MyIp {

        [DataMember(Name = "ipaddress")]
        public string IP { get; set; }

        [DataMember(Name = "continent_code")]
        public string ContinentCode { get; set; }

        [DataMember(Name = "continent_name")]
        public string ContinentName { get; set; }

        [DataMember(Name = "country_code")]
        public string CountryCode { get; set; }

        [DataMember(Name = "country_name")]
        public string CountryName { get; set; }
    }
}
