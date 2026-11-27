// Pure: the video's timeline. One audio track (clips and silences) and, in step with it, one still picture per
// spoken piece. Gaps are the listening track's own (tools/dino/DinoReader.BookBuilder/ListenTrackPlan.cs):
// 150 ms between pieces, 900 ms at a page turn. Frame counts come from cumulative boundaries, so the picture
// never drifts from the audio by more than half a frame, however long the book.

export const FPS = 30;
export const SENTENCE_GAP_SEC = 0.15;
export const PAGE_GAP_SEC = 0.9;
export const OPENING_MIN_SEC = 4;
export const OPENING_TAIL_SEC = 1.2;
export const CLOSING_MIN_SEC = 9;
export const CLOSING_LEAD_SEC = 1;
export const CLOSING_TAIL_SEC = 4;
export const FADE_FRAMES = 12;

const frameAt = (sec) => Math.round(sec * FPS);

/**
 * @param {{ openingVoice?: {file:string,sec:number}, closingVoice?: {file:string,sec:number},
 *           pages: {page:number, pieces:{index:number, clip:string, sec:number}[]}[] }} input
 * @returns {{ scenes: object[], audio: object[], totalSec: number, totalFrames: number }}
 */
export function buildTimeline({ openingVoice, closingVoice, pages }) {
  const audio = [];
  const scenes = [];
  const cut = { sec: 0 }; // running end of the last segment, in seconds
  const addStill = (scene, key, durationSec, extra = {}) => {
    const start = frameAt(cut.sec);
    cut.sec += durationSec;
    scene.stills.push({ key, frames: frameAt(cut.sec) - start, startSec: cut.sec - durationSec, ...extra });
  };

  scenes.push(openingScene({ openingVoice, audio, addStill }));
  addPageScenes({ pages, scenes, audio, addStill });
  scenes.push(closingScene({ closingVoice, audio, addStill }));

  const totalFrames = scenes.flatMap((s) => s.stills).reduce((sum, s) => sum + s.frames, 0);
  return { scenes, audio, totalSec: cut.sec, totalFrames };
}

function openingScene({ openingVoice, audio, addStill }) {
  const opening = { id: 'opening', kind: 'opening', stills: [] };
  const openingSec = Math.max(OPENING_MIN_SEC, (openingVoice?.sec ?? 0) + OPENING_TAIL_SEC);
  if (openingVoice) audio.push({ file: openingVoice.file, kind: 'voice' }, { silence: openingSec - openingVoice.sec });
  else audio.push({ silence: openingSec });
  addStill(opening, 'opening', openingSec);
  return opening;
}

function addPageScenes({ pages, scenes, audio, addStill }) {
  pages.forEach((group, groupIndex) => {
    const scene = { id: `page-${group.page + 1}`, kind: 'page', page: group.page, stills: [] };
    const lastGroup = groupIndex === pages.length - 1;
    group.pieces.forEach((piece, i) => {
      const lastPiece = i === group.pieces.length - 1;
      const gap = lastPiece ? PAGE_GAP_SEC : SENTENCE_GAP_SEC;
      audio.push({ file: piece.clip, kind: 'clip', index: piece.index }, { silence: gap });
      addStill(scene, `page-${group.page}-piece-${piece.index}`, piece.sec + gap, { pieceIndex: piece.index, lastOfBook: lastGroup && lastPiece });
    });
    scenes.push(scene);
  });
}

function closingScene({ closingVoice, audio, addStill }) {
  const closing = { id: 'closing', kind: 'closing', stills: [] };
  const voiceSec = closingVoice?.sec ?? 0;
  const closingSec = Math.max(CLOSING_MIN_SEC, CLOSING_LEAD_SEC + voiceSec + CLOSING_TAIL_SEC);
  audio.push({ silence: CLOSING_LEAD_SEC });
  if (closingVoice) audio.push({ file: closingVoice.file, kind: 'voice' });
  audio.push({ silence: closingSec - CLOSING_LEAD_SEC - voiceSec });
  addStill(closing, 'closing', closingSec);
  return closing;
}

export function sceneFrames(scene) {
  return scene.stills.reduce((sum, still) => sum + still.frames, 0);
}
