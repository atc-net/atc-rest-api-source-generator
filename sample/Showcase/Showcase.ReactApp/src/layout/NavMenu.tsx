import { useState } from 'react';
import { useNavigate, useLocation } from 'react-router';
import {
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Collapse,
} from '@mui/material';
import HomeIcon from '@mui/icons-material/Home';
import AccountCircleIcon from '@mui/icons-material/AccountCircle';
import ListIcon from '@mui/icons-material/List';
import TableChartIcon from '@mui/icons-material/TableChart';
import StreamIcon from '@mui/icons-material/Stream';
import TaskIcon from '@mui/icons-material/Task';
import PeopleIcon from '@mui/icons-material/People';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import FolderIcon from '@mui/icons-material/Folder';
import DownloadIcon from '@mui/icons-material/Download';
import UploadIcon from '@mui/icons-material/Upload';
import NotificationsIcon from '@mui/icons-material/Notifications';
import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive';
import SubscriptionsIcon from '@mui/icons-material/Subscriptions';
import WebhookIcon from '@mui/icons-material/Webhook';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import BugReportIcon from '@mui/icons-material/BugReport';
import ErrorIcon from '@mui/icons-material/Error';
import ExpandLess from '@mui/icons-material/ExpandLess';
import ExpandMore from '@mui/icons-material/ExpandMore';

interface NavGroup {
  title: string;
  icon: React.ReactElement;
  items: { label: string; href: string; icon: React.ReactElement }[];
}

const navGroups: NavGroup[] = [
  {
    title: 'Accounts',
    icon: <AccountCircleIcon />,
    items: [
      { label: 'List Accounts', href: '/accounts', icon: <ListIcon /> },
      { label: 'Paginated', href: '/accounts/paginated', icon: <TableChartIcon /> },
      { label: 'Async Enumerable', href: '/accounts/async-enumerable', icon: <StreamIcon /> },
    ],
  },
  {
    title: 'Tasks',
    icon: <TaskIcon />,
    items: [
      { label: 'List Tasks', href: '/tasks', icon: <ListIcon /> },
    ],
  },
  {
    title: 'Users',
    icon: <PeopleIcon />,
    items: [
      { label: 'List Users', href: '/users', icon: <ListIcon /> },
      { label: 'Create User', href: '/users/create', icon: <PersonAddIcon /> },
    ],
  },
  {
    title: 'Files',
    icon: <FolderIcon />,
    items: [
      { label: 'Download Files', href: '/files', icon: <DownloadIcon /> },
      { label: 'Upload Files', href: '/files/upload', icon: <UploadIcon /> },
    ],
  },
  {
    title: 'Notifications',
    icon: <NotificationsIcon />,
    items: [
      { label: 'Live Feed', href: '/notifications', icon: <NotificationsActiveIcon /> },
      { label: 'Subscriptions', href: '/notifications/subscriptions', icon: <SubscriptionsIcon /> },
    ],
  },
  {
    title: 'Webhooks',
    icon: <WebhookIcon />,
    items: [
      { label: 'Demo', href: '/webhooks/demo', icon: <PlayArrowIcon /> },
    ],
  },
  {
    title: 'Testing',
    icon: <BugReportIcon />,
    items: [
      { label: 'Exception Handling', href: '/testing/exceptions', icon: <ErrorIcon /> },
    ],
  },
];

export function NavMenu() {
  const navigate = useNavigate();
  const location = useLocation();
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>(
    Object.fromEntries(navGroups.map((g) => [g.title, true])),
  );

  const toggleGroup = (title: string) => {
    setOpenGroups((prev) => ({ ...prev, [title]: !prev[title] }));
  };

  return (
    <List component="nav" dense>
      <ListItemButton
        selected={location.pathname === '/'}
        onClick={() => navigate('/')}
      >
        <ListItemIcon><HomeIcon /></ListItemIcon>
        <ListItemText primary="Home" />
      </ListItemButton>

      {navGroups.map((group) => (
        <div key={group.title}>
          <ListItemButton onClick={() => toggleGroup(group.title)}>
            <ListItemIcon>{group.icon}</ListItemIcon>
            <ListItemText primary={group.title} />
            {openGroups[group.title] ? <ExpandLess /> : <ExpandMore />}
          </ListItemButton>
          <Collapse in={openGroups[group.title]} timeout="auto" unmountOnExit>
            <List component="div" disablePadding>
              {group.items.map((item) => (
                <ListItemButton
                  key={item.href}
                  sx={{ pl: 4 }}
                  selected={location.pathname === item.href}
                  onClick={() => navigate(item.href)}
                >
                  <ListItemIcon>{item.icon}</ListItemIcon>
                  <ListItemText primary={item.label} />
                </ListItemButton>
              ))}
            </List>
          </Collapse>
        </div>
      ))}
    </List>
  );
}