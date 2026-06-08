from datetime import datetime
from typing import List, Optional
from sqlmodel import Field, Relationship, SQLModel
from .tablice_uzytkownicy import TabliceUzytkownicy

class Uzytkownicy(SQLModel, table=True):
    __tablename__ = "uzytkownicy"

    id_uzytkownika: Optional[int] = Field(default=None, primary_key=True)
    email: str = Field(unique=True, index=True)
    password_hash: Optional[str] = None
    nazwa_uzytkownika: str
    avatar_url: Optional[str] = None
    github_id: Optional[str] = None
    google_id: Optional[str] = None
    github_refresh_token_encrypted: Optional[str] = None
    google_refresh_token_encrypted: Optional[str] = None
    data_rejestracji: datetime = Field(default_factory=datetime.now(timezone.utc))

    stworzone_tablice: List["Tablice"] = Relationship(back_populates="owner")
    tablice_wspolpracownik: List["Tablice"] = Relationship(
        back_populates="czlonkowie", link_model=TabliceUzytkownicy
    )
    stworzone_zadania: List["Zadania"] = Relationship(
        back_populates="tworca", 
        sa_relationship_kwargs={"foreign_keys": "Zadania.id_uzytkownika_tworcy_zadania"}
    )
    przypisane_zadania: List["Zadania"] = Relationship(
        back_populates="przypisany_uzytkownik", 
        sa_relationship_kwargs={"foreign_keys": "Zadania.id_uzytkownika_przypisanego"}
    )
    komentarze: List["Komentarze"] = Relationship(back_populates="autor")