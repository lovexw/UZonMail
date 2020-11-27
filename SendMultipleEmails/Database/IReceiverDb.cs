﻿using SendMultipleEmails.Datas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SendMultipleEmails.Database
{
    interface IReceiverDb
    {
        bool InsertReceiver(Receiver obj);
        bool InsertReceivers(IEnumerable<Receiver> objs);

        bool DeleteReceiver(int id);

        bool DeleteAllReceivers();

        Receiver FindOneReceiverByName(string name);

        Receiver FindOneReceiverByEmail(string email);

        IEnumerable<Receiver> FindAllReceivers();

        bool UpdateReceiver(Receiver obj);

        int UpsertReceivers(IEnumerable<Receiver> objs);
    }
}
