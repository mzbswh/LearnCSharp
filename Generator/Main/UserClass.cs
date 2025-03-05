using System.ComponentModel;

namespace GeneratedNamespace
{
    public partial class UserClass
    {
        [AutoNotify]
        private bool _boolProp;

        [AutoNotify(PropertyName = "Count")]
        private int _intProp;


        [GeneratedNamespace.Generated]
        public partial void UserMethod();
    }
}