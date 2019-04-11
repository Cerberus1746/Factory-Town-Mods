using System;

namespace UnityModManagerNet {
    public partial class UnityModManager {

        public partial class Repository {
            public Release[] releases;
            [Serializable]
            public class Release : IEquatable<Release> {
                public string Id;
                public string Version;
                public string DownloadUrl;

                public bool Equals(Release other) => this.Id.Equals(other.Id);

                public override bool Equals(object obj) {
                    if (obj is null) {
                        return false;
                    }
                    return obj is Release obj2 && this.Equals(obj2);
                }

                public override int GetHashCode() => this.Id.GetHashCode();
            }
        }
    }
}
