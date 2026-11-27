// Pure: each video's YouTube title, description, tags and language, and each language's playlist title, all in the
// video's own language (Slice 435 item 5). Kids status and the AI flag are set by the upload script, from config.

import { fill, stringsFor } from './strings.mjs';
import { SITE_URL } from './frame.mjs';
import { creditsIn, titleIn } from './story.mjs';

export const TITLE_LIMIT = 100;
export const DESCRIPTION_LIMIT = 5000;
const YOUTUBE_LANGUAGE = { zh: 'zh-Hans' };
export const youtubeLanguage = (language) => YOUTUBE_LANGUAGE[language] ?? language;

export function videoMetadata(story, language) {
  const strings = stringsFor(language);
  const title = titleIn(story, language);
  const videoTitle = `${title} | ${strings.kids}`;
  if (videoTitle.length > TITLE_LIMIT) throw new Error(`Video title over ${TITLE_LIMIT} characters: ${videoTitle}`);
  const description = [
    fill(strings.intro, { title }),
    '',
    creditsIn(story, language),
    strings.ai,
    '',
    strings.grownUps,
    SITE_URL,
  ].join('\n');
  return {
    title: videoTitle,
    description,
    tags: [title, 'Dino Reader', strings.kids],
    defaultLanguage: youtubeLanguage(language),
    defaultAudioLanguage: youtubeLanguage(language),
    thumbnailTag: strings.kids,
  };
}

export function playlistTitle(language) {
  const strings = stringsFor(language);
  return fill(strings.playlist, { name: strings.name });
}
