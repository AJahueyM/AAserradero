// Dev environment parameters for a subscription-scoped deployment (creates the resource group).
// Deploy with:
//   az deployment sub create --name aa-dev --location westus2 \
//     --template-file ../main.sub.bicep --parameters params/dev.bicepparam \
//     --parameters initialDeployment=true containerImage=mcr.microsoft.com/k8se/quickstart:latest
// Then, after AcrPull is granted and the real image is pushed, re-deploy with
//   --parameters initialDeployment=false containerImage=<acr-login-server>/antiguoaserradero:<tag>
using '../main.sub.bicep'

param location = 'westus2'
param sqlLocation = 'westus3'
param staticWebAppLocation = 'westus2'
param environmentName = 'dev'
param namePrefix = 'aareserva'
param resourceGroupName = 'rg-aareserva-dev'

// Microsoft Entra SQL administrator (the signed-in deploying user).
param aadAdminLogin = 'albertojahueymoncada_gmail.com#EXT#@albertojahueymoncadagmail.onmicrosoft.com'
param aadAdminObjectId = 'b1f37b28-cb84-41bc-971b-6cac5fb2ac76'
param aadAdminPrincipalType = 'User'

// App registration (API app used for token validation + Graph app-role resolution).
param azureAdClientId = 'a1fe5dd6-eb0b-42a1-8577-7207096f2768'
param azureAdAudience = 'api://a1fe5dd6-eb0b-42a1-8577-7207096f2768'
param graphApiClientAppId = 'a1fe5dd6-eb0b-42a1-8577-7207096f2768'

param aspnetcoreEnvironment = 'Production'
param acsSenderAddress = 'DoNotReply@placeholder.azurecomm.net'

// Overridden on the CLI per deployment phase.
param initialDeployment = true
param containerImage = 'mcr.microsoft.com/k8se/quickstart:latest'
