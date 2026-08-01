import { HttpContextToken } from '@angular/common/http';

export const SKIP_GLOBAL_ERROR_NOTIFICATION = new HttpContextToken<boolean>(() => false);