﻿using System.Collections.Generic;

namespace Library.Programming.Contract
{
    public class ProgrammingConfigurationContract
    {
        public List<ProgrammingLanguageContract> Languages { get; internal set; }
        public List<ProgrammingJobContract> Jobs { get; internal set; }
        public List<ProgrammingTechnologyStackContract> TechnologyStacks { get; internal set; }
        public List<ProgrammingProjectTypeContract> ProjectTypes { get; internal set; }
    }
}