using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Admin.Tests.Services
{
    public partial class AdminVirtualKeyServiceTests
    {
        // This file contains ValidateVirtualKeyAsync tests split across multiple partial class files:
        // - AdminVirtualKeyServiceTests.Validate.BasicValidation.cs
        // - AdminVirtualKeyServiceTests.Validate.ModelRestrictions.cs  
        // - AdminVirtualKeyServiceTests.Validate.Generation.cs
        // - AdminVirtualKeyServiceTests.Validate.SpecialCases.cs
    }
}