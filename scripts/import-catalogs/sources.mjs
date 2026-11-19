// Pinned source URLs for the catalog importers. Change SRD_SHA deliberately and re-run the tests.

export const SPELLS_BASE = 'https://avribacki.gitlab.io/magias5e';
export const SPELLS_URL = `${SPELLS_BASE}/spells.js`;
export const SPELLS_BY_CLASS_URL = `${SPELLS_BASE}/spells-by-class.js`;

export const SRD_REPO = '5e-bits/5e-srd-api';
/** main of 5e-bits/5e-srd-api on 2026-11-19 (commit date 2026-11-19T01:02:36Z). */
export const SRD_SHA = 'c33bc0aab99716eb60b094222533d5531405eef4';
export const SRD_DIR = 'packages/5e-database/src/2024/en';
export const srdUrl = (file) => `https://raw.githubusercontent.com/${SRD_REPO}/${SRD_SHA}/${SRD_DIR}/${file}`;
export const SRD_FILES = {
  equipment: '5e-SRD-Equipment.json',
  magicItems: '5e-SRD-Magic-Items.json',
  weaponProperties: '5e-SRD-Weapon-Properties.json',
  weaponMastery: '5e-SRD-Weapon-Mastery-Properties.json',
};
export const srdCacheName = (file) => `srd-${SRD_SHA.slice(0, 12)}/${file}`;

export const SRD_ATTRIBUTION =
  'This work includes material from the System Reference Document 5.2.1 ("SRD 5.2.1") by Wizards of the Coast LLC, ' +
  'available at https://www.dndbeyond.com/srd. The SRD 5.2.1 is licensed under the Creative Commons Attribution 4.0 ' +
  'International License, available at https://creativecommons.org/licenses/by/4.0/legalcode.';
