using System.Runtime.Serialization;

namespace ChainableSearch.Services
{
    [DataContract]
    public class ResponseInfo
    {
        [DataMember]
        public bool IsSuccessful { get; set; }

        [DataMember]
        public string Data { get; set; }

        [DataMember]
        public string ErrorMessage { get; set; }

        [DataMember]
        public string ErrorException{ get; set; }
    }
}
