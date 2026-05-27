import { Card, CardContent, CardActionArea, Grid2, Typography, Box } from '@mui/material';
import { useNavigate } from 'react-router';
import AccountBalanceIcon from '@mui/icons-material/AccountBalance';
import TaskAltIcon from '@mui/icons-material/TaskAlt';
import PeopleIcon from '@mui/icons-material/People';
import FolderIcon from '@mui/icons-material/Folder';

const sections = [
  {
    title: 'Accounts',
    description: 'CRUD operations, paginated queries, and async streaming.',
    icon: <AccountBalanceIcon sx={{ fontSize: 48 }} color="primary" />,
    path: '/accounts',
  },
  {
    title: 'Tasks',
    description: 'Simple task management with create, list, and delete.',
    icon: <TaskAltIcon sx={{ fontSize: 48 }} color="success" />,
    path: '/tasks',
  },
  {
    title: 'Users',
    description: 'Full user management with search, filtering, and profile editing.',
    icon: <PeopleIcon sx={{ fontSize: 48 }} color="secondary" />,
    path: '/users',
  },
  {
    title: 'Files',
    description: 'File download, preview, and multi-file upload with metadata.',
    icon: <FolderIcon sx={{ fontSize: 48 }} color="warning" />,
    path: '/files',
  },
];

export default function IndexPage() {
  const navigate = useNavigate();

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Showcase Dashboard
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 4 }}>
        Explore the auto-generated REST API client in action. Each section demonstrates a different
        capability of the source generator.
      </Typography>

      <Grid2 container spacing={3}>
        {sections.map((section) => (
          <Grid2 key={section.path} size={{ xs: 12, sm: 6, md: 3 }}>
            <Card sx={{ height: '100%' }}>
              <CardActionArea
                onClick={() => navigate(section.path)}
                sx={{ height: '100%', p: 2 }}
              >
                <CardContent sx={{ textAlign: 'center' }}>
                  {section.icon}
                  <Typography variant="h6" sx={{ mt: 2 }}>
                    {section.title}
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                    {section.description}
                  </Typography>
                </CardContent>
              </CardActionArea>
            </Card>
          </Grid2>
        ))}
      </Grid2>
    </Box>
  );
}
