import { notifications } from '@mantine/notifications'

/**
 * Toasts, wrapped so every call site agrees on colour and timing.
 *
 * Errors stay up longer and do not auto-close on the destructive paths: an admin who
 * missed why a delete was refused has to guess, and the API's messages are written to be
 * read (SaveItem explains *which* rule a modifier broke).
 */

export function notifySuccess(message: string, title = 'Done') {
  notifications.show({ title, message, color: 'teal', autoClose: 3000 })
}

export function notifyError(message: string, title = 'That did not work') {
  notifications.show({ title, message, color: 'red', autoClose: 8000 })
}

/** Pulls the useful sentence out of whatever the client threw. */
export function messageFrom(error: unknown): string {
  if (error instanceof Error && error.message !== '') return error.message
  if (typeof error === 'string' && error !== '') return error

  return 'Something went wrong.'
}
