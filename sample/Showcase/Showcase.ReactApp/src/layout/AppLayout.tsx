import { useState } from 'react';
import { Outlet } from 'react-router';
import {
  AppBar,
  Toolbar,
  IconButton,
  Typography,
  Drawer,
  Box,
  Container,
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import LightModeIcon from '@mui/icons-material/LightMode';
import DarkModeIcon from '@mui/icons-material/DarkMode';
import CodeIcon from '@mui/icons-material/Code';
import { useThemeContext } from '../theme/ThemeContext';
import { NavMenu } from './NavMenu';

const DRAWER_WIDTH = 260;

export function AppLayout() {
  const [drawerOpen, setDrawerOpen] = useState(true);
  const { isDarkMode, toggleDarkMode } = useThemeContext();

  return (
    <Box sx={{ display: 'flex' }}>
      <AppBar
        position="fixed"
        sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}
        elevation={1}
      >
        <Toolbar>
          <IconButton
            color="inherit"
            edge="start"
            onClick={() => setDrawerOpen(!drawerOpen)}
          >
            <MenuIcon />
          </IconButton>
          <Typography variant="h6" sx={{ ml: 2, flexGrow: 1 }}>
            Showcase API Explorer
          </Typography>
          <IconButton color="inherit" onClick={toggleDarkMode}>
            {isDarkMode ? <LightModeIcon /> : <DarkModeIcon />}
          </IconButton>
          <IconButton
            color="inherit"
            href="https://github.com/atc-net"
            target="_blank"
          >
            <CodeIcon />
          </IconButton>
        </Toolbar>
      </AppBar>
      <Drawer
        variant="persistent"
        open={drawerOpen}
        sx={{
          width: DRAWER_WIDTH,
          flexShrink: 0,
          '& .MuiDrawer-paper': {
            width: DRAWER_WIDTH,
            boxSizing: 'border-box',
            mt: '64px',
          },
        }}
      >
        <NavMenu />
      </Drawer>
      <Box
        component="main"
        sx={{
          flexGrow: 1,
          pt: '80px',
          px: 3,
          ml: drawerOpen ? 0 : `-${DRAWER_WIDTH}px`,
          transition: 'margin 225ms cubic-bezier(0, 0, 0.2, 1)',
        }}
      >
        <Container maxWidth="xl">
          <Outlet />
        </Container>
      </Box>
    </Box>
  );
}