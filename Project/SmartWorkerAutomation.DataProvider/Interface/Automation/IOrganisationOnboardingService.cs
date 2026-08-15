using SmartWorkerAutomation.Common.Automation;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWorkerAutomation.DataProvider.Interface.Automation;

public interface IOrganisationOnboardingService
{
    Task<OnboardOrganisationResponse> OnboardAsync(OnboardOrganisationRequest request);
}
