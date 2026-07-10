import {
  BrowserCacheLocation,
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AccountInfo,
  type Configuration,
  type IPublicClientApplication,
} from '@azure/msal-browser';

function requiredEnv(name: keyof ImportMetaEnv): string {
  const value = import.meta.env[name];
  if (!value) throw new Error(`Missing required environment variable: ${name}`);
  return value;
}

export const apiScope = requiredEnv('VITE_API_SCOPE');
export const loginRequest = { scopes: [apiScope] };

const authority = requiredEnv('VITE_AAD_AUTHORITY');

// CIAM / External ID authorities (*.ciamlogin.com) are not part of the public cloud list, so MSAL
// requires the authority host to be declared as a known authority.
function knownAuthorityHost(authorityUrl: string): string[] {
  try {
    return [new URL(authorityUrl).host];
  } catch {
    return [];
  }
}

export const msalConfig: Configuration = {
  auth: {
    clientId: requiredEnv('VITE_AAD_CLIENT_ID'),
    authority,
    knownAuthorities: knownAuthorityHost(authority),
    redirectUri: requiredEnv('VITE_AAD_REDIRECT_URI'),
    postLogoutRedirectUri: requiredEnv('VITE_AAD_REDIRECT_URI'),
  },
  cache: { cacheLocation: BrowserCacheLocation.SessionStorage },
};

export const msalInstance = new PublicClientApplication(msalConfig);

export async function acquireApiAccessToken(
  instance: IPublicClientApplication,
  account: AccountInfo,
): Promise<string> {
  try {
    const result = await instance.acquireTokenSilent({ ...loginRequest, account });
    return result.accessToken;
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) await instance.loginRedirect(loginRequest);
    throw error;
  }
}
