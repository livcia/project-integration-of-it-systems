from datetime import datetime
from typing import Optional
from sqlmodel import Field, Relationship, SQLModel

class Komentarze(SQLModel, table=True):
    __tablename__ = "komentarze"

    id_komentarza: Optional[int] = Field(default=None, primary_key=True)
    id_zadania: int = Field(foreign_key="zadania.id_zadania")
    tresc_komentarza: str
    id_uzytkownika: int = Field(foreign_key="uzytkownicy.id_uzytkownika")
    data_utworzenia: datetime = Field(default_factory=datetime.now(timezone.utc))
    data_edycji: Optional[datetime] = None

    zadanie: "Zadania" = Relationship(back_populates="komentarze")
    autor: "Uzytkownicy" = Relationship(back_populates="komentarze")