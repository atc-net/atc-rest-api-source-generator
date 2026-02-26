import { useMutation } from '@tanstack/react-query';
import { filesClient } from '../config/api';
import { ApiError } from '../api/errors/ApiError';

export function useFileDownload() {
  return useMutation({
    mutationFn: async (id: string) => {
      const result = await filesClient.getFileById(id);
      if (result.status === 'ok') {
        return result.data;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to download file',
        result.response,
      );
    },
  });
}

export function useUploadSingleFile() {
  return useMutation({
    mutationFn: async (file: Blob | File) => {
      const result = await filesClient.uploadSingleFileAsFormData(file);
      if (result.status === 'noContent' || result.status === 'ok') {
        return;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to upload file',
        result.response,
      );
    },
  });
}

export function useUploadMultiFiles() {
  return useMutation({
    mutationFn: async (files: (Blob | File)[]) => {
      const result = await filesClient.uploadMultiFilesAsFormData(files);
      if (result.status === 'noContent' || result.status === 'ok') {
        return;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to upload files',
        result.response,
      );
    },
  });
}

export function useUploadWithMetadata() {
  return useMutation({
    mutationFn: async (data: { itemName?: string; file?: Blob | File; items?: string[] }) => {
      const result = await filesClient.uploadSingleObjectWithFileAsFormData(data);
      if (result.status === 'noContent' || result.status === 'ok') {
        return;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to upload file with metadata',
        result.response,
      );
    },
  });
}

export function useUploadMultiWithMetadata() {
  return useMutation({
    mutationFn: async (data: { files?: (Blob | File)[] }) => {
      const result = await filesClient.uploadSingleObjectWithFilesAsFormData(data);
      if (result.status === 'noContent' || result.status === 'ok') {
        return;
      }
      throw new ApiError(
        result.response.status,
        result.response.statusText,
        'error' in result ? result.error.message : 'Failed to upload files with metadata',
        result.response,
      );
    },
  });
}
