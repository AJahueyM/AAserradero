import React from 'react';
import ReactDOM from 'react-dom/client';
import { msalInstance } from './auth/msalConfig';
import { App } from './app/App';
import './i18n';

async function bootstrap() {
  await msalInstance.initialize();

  try {
    const redirectResult = await msalInstance.handleRedirectPromise();
    const account = redirectResult?.account ?? msalInstance.getAllAccounts()[0];
    if (account) {
      msalInstance.setActiveAccount(account);
    }
  } catch (error) {
    sessionStorage.setItem('auth.redirectError', JSON.stringify(toAuthErrorSnapshot(error)));
  }

  ReactDOM.createRoot(document.getElementById('root') as HTMLElement).render(
    <React.StrictMode>
      <App msalInstance={msalInstance} />
    </React.StrictMode>,
  );
}

function toAuthErrorSnapshot(error: unknown) {
  if (error instanceof Error) return { name: error.name, message: error.message };
  return { name: 'AuthError', message: String(error) };
}

void bootstrap();
