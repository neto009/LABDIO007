using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fnPaymanet.Model
{
    internal class PaymantModel
    {
        public string id { get { return Guid.NewGuid().ToString(); } }
        public string idPayment { get { return Guid.NewGuid().ToString(); } }
        public string nome { get; set; }
        public string email { get; set; }
        public string modelo { get; set; }
        public int ano { get; set; }
        public string tempoAluguel { get; set; }
        public DateTime data { get; set; }
        public string Status { get; set; }
        public DateTime? DataAprovacao { get; set; }
    }
}
