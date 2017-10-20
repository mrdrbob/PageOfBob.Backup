using System.IO;
using System.Threading.Tasks;

namespace PageOfBob.Backup
{
    public delegate Task ProcessStream(Stream stream);
}
