import { describeNameTagCrud } from '../fixtures/name-tag-crud';
import { TasksPage } from '../page-objects/tasks.page';

// createTask, listTasks, updateTaskById, getTaskById and deleteTaskById
describeNameTagCrud('Tasks', '/tasks', (page) => new TasksPage(page));
