import { createContext, useContext, useMemo, useState } from 'react';
import {
  ThemeProvider as MuiThemeProvider,
  CssBaseline,
  useMediaQuery,
} from '@mui/material';
import { lightTheme, darkTheme } from './theme';

interface ThemeContextValue {
  isDarkMode: boolean;
  toggleDarkMode: () => void;
}

const ThemeContext = createContext<ThemeContextValue>({
  isDarkMode: true,
  toggleDarkMode: () => {},
});

export const useThemeContext = () => useContext(ThemeContext);

export function AppThemeProvider({ children }: { children: React.ReactNode }) {
  const prefersDark = useMediaQuery('(prefers-color-scheme: dark)');
  const [isDarkMode, setIsDarkMode] = useState(prefersDark);
  const toggleDarkMode = () => setIsDarkMode((prev) => !prev);

  const theme = useMemo(
    () => (isDarkMode ? darkTheme : lightTheme),
    [isDarkMode],
  );

  return (
    <ThemeContext value={{ isDarkMode, toggleDarkMode }}>
      <MuiThemeProvider theme={theme}>
        <CssBaseline />
        {children}
      </MuiThemeProvider>
    </ThemeContext>
  );
}