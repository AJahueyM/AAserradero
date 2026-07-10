/*
Run this in the deployed Azure SQL database as the Microsoft Entra SQL admin.
Replace the user name with the Container App system-assigned managed identity
display name if needed. The Container App principalId is output by main.bicep.
*/

CREATE USER [<container-app-managed-identity-name>] FROM EXTERNAL PROVIDER;

ALTER ROLE db_datareader ADD MEMBER [<container-app-managed-identity-name>];
ALTER ROLE db_datawriter ADD MEMBER [<container-app-managed-identity-name>];
ALTER ROLE db_ddladmin ADD MEMBER [<container-app-managed-identity-name>];
