// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces
declare global {
	namespace App {
		// interface Error {}
		// interface Locals {}
		// interface PageData {}
		// interface PageState {}

		// adapter-cloudflare exposes Pages/Worker env vars on `platform.env`. The gist OAuth route
		// (/api/auth/github) reads GITHUB_CLIENT_ID + GITHUB_CLIENT_SECRET from here (the secret is a
		// dashboard-managed encrypted var — never committed; T-49-SECRET). SITE_ORIGIN pins the OAuth
		// redirect origin to a server-known constant instead of the request Host (CR-02).
		interface Platform {
			env?: {
				GITHUB_CLIENT_ID?: string;
				GITHUB_CLIENT_SECRET?: string;
				SITE_ORIGIN?: string;
			};
		}
	}
}

export {};
