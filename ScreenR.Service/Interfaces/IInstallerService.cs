﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenR.Service.Interfaces
{
    public interface IInstallerService
    {
        Task Install();
        Task Uninstall();
    }
}
