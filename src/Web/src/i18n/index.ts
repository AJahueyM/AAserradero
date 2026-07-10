import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { esMx } from './resources/es-MX';
void i18n
  .use(initReactI18next)
  .init({
    lng: 'es-MX',
    fallbackLng: 'es-MX',
    supportedLngs: ['es-MX'],
    resources: { 'es-MX': esMx },
    interpolation: { escapeValue: false },
  });
export default i18n;
