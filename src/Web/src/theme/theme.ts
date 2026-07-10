import { createTheme } from '@mui/material/styles';
export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { light: '#273c18', main: '#395723', dark: '#60784f', contrastText: '#ffffff' },
    secondary: { light: '#af974b', main: '#fbd86c', dark: '#fbdf89', contrastText: '#1f1f1f' },
    background: { default: '#f7f6f1', paper: '#ffffff' },
  },
  typography: {
    fontFamily: ['Inter', 'Roboto', 'Arial', 'sans-serif'].join(','),
    h1: { fontWeight: 700 },
    h2: { fontWeight: 700 },
    h3: { fontWeight: 700 },
    h4: { fontWeight: 700 },
    h5: { fontWeight: 700 },
    h6: { fontWeight: 700 },
    button: { textTransform: 'none', fontWeight: 700 },
  },
  shape: { borderRadius: 12 },
  components: { MuiButton: { defaultProps: { disableElevation: true } } },
});
